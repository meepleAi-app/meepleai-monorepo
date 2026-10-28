using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// La fixture di <see cref="MechanicAnalysisStaleDraftIntegrationTests"/>: host condiviso per la classe, database
/// fresco per test (#4050).
/// </summary>
public sealed class MechanicAnalysisStaleDraftHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "mech_stale_4503");

/// <summary>
/// #4503, sul Postgres vero. Una analisi rimasta in <c>Draft</c> perche' la pipeline e' morta con il processo tiene
/// occupato lo slot (gioco, PDF, prompt) dell'indice unico filtrato <c>ux_mechanic_analyses_shared_game_pdf_prompt</c>
/// (<c>status &lt;&gt; 3</c>), e nessuno la chiudeva. Il comando la rifiuta, e lo slot torna libero.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisStaleDraftIntegrationTests
    : IClassFixture<MechanicAnalysisStaleDraftHostFixture>, IAsyncLifetime
{
    private const string Prompt = "v1-stale-4503";

    private readonly MechanicAnalysisStaleDraftHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _adminId;

    public MechanicAnalysisStaleDraftIntegrationTests(MechanicAnalysisStaleDraftHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_adminId, _) = await TestSessionHelper.CreateUserSessionAsync(db, Guid.NewGuid());
    }

    // Host, client e database appartengono alla fixture.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ExpireStaleDraftsResult> ExpireAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new ExpireStaleMechanicAnalysisDraftsCommand());
    }

    [Fact]
    public async Task AnOrphanedDraft_IsRejected_AndItsSlotIsFreeAgain()
    {
        var (gameId, pdfId) = await SeedGameWithPdfAsync();
        var orphanId = await InsertAnalysisAsync(gameId, pdfId, MechanicAnalysisStatus.Draft, HoursAgo(3));

        // Il difetto: finche' il Draft c'e', un'altra analisi per lo stesso (gioco, PDF, prompt) non entra.
        var blocked = () => InsertAnalysisAsync(gameId, pdfId, MechanicAnalysisStatus.Draft, HoursAgo(0));
        await blocked.Should().ThrowAsync<DbUpdateException>("the orphan holds the filtered unique index");

        var result = await ExpireAsync();

        result.Should().Be(new ExpireStaleDraftsResult(Found: 1, Expired: 1));
        var (status, reason, reviewedBy) = await ReadAsync(orphanId);
        status.Should().Be(MechanicAnalysisStatus.Rejected);
        reason.Should().Be("stale_draft_expired");
        reviewedBy.Should().Be(_adminId, "the creating admin is recorded as actor, as the executor does");

        var retry = () => InsertAnalysisAsync(gameId, pdfId, MechanicAnalysisStatus.Draft, HoursAgo(0));
        await retry.Should().NotThrowAsync("a Rejected row no longer counts for the unique index");
    }

    [Fact]
    public async Task ARecentDraft_AndEveryOtherStatus_AreLeftAlone()
    {
        var (recentGame, recentPdf) = await SeedGameWithPdfAsync();
        var recent = await InsertAnalysisAsync(recentGame, recentPdf, MechanicAnalysisStatus.Draft, HoursAgo(0.25));

        // Vecchie quanto basta, ma non in Draft: non sono orfane, la pipeline le ha portate avanti.
        var (reviewGame, reviewPdf) = await SeedGameWithPdfAsync();
        var inReview = await InsertAnalysisAsync(reviewGame, reviewPdf, MechanicAnalysisStatus.InReview, HoursAgo(5));
        var (rejectedGame, rejectedPdf) = await SeedGameWithPdfAsync();
        var rejected = await InsertAnalysisAsync(rejectedGame, rejectedPdf, MechanicAnalysisStatus.Rejected, HoursAgo(5));

        var result = await ExpireAsync();

        result.Should().Be(new ExpireStaleDraftsResult(Found: 0, Expired: 0));
        (await ReadAsync(recent)).Status.Should().Be(MechanicAnalysisStatus.Draft);
        (await ReadAsync(inReview)).Status.Should().Be(MechanicAnalysisStatus.InReview);
        (await ReadAsync(rejected)).Status.Should().Be(MechanicAnalysisStatus.Rejected);
    }

    [Fact]
    public async Task TheListing_IsOldestFirst_BoundedByTheBatch_AndStrictAtTheCutoff()
    {
        var oldest = await SeedDraftAsync(HoursAgo(9));
        var middle = await SeedDraftAsync(HoursAgo(6));
        var newest = await SeedDraftAsync(HoursAgo(4));
        var cutoff = DateTime.UtcNow.AddHours(-2);
        var onTheCutoff = await SeedDraftAsync(cutoff);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMechanicAnalysisRepository>();

        var two = await repository.GetDraftIdsCreatedBeforeAsync(cutoff, maxCount: 2);
        var all = await repository.GetDraftIdsCreatedBeforeAsync(cutoff, maxCount: 50);

        two.Should().Equal(oldest, middle);
        all.Should().Equal(oldest, middle, newest);
        all.Should().NotContain(onTheCutoff, "a draft created exactly at the cutoff is not older than it");
    }

    private static DateTime HoursAgo(double hours) => DateTime.UtcNow.AddHours(-hours);

    private async Task<Guid> SeedDraftAsync(DateTime createdAt)
    {
        var (gameId, pdfId) = await SeedGameWithPdfAsync();
        return await InsertAnalysisAsync(gameId, pdfId, MechanicAnalysisStatus.Draft, createdAt);
    }

    private async Task<(MechanicAnalysisStatus Status, string? Reason, Guid? ReviewedBy)> ReadAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var row = await db.Set<MechanicAnalysisEntity>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.Id == id)
            .Select(a => new { a.Status, a.RejectionReason, a.ReviewedBy })
            .SingleAsync();
        return ((MechanicAnalysisStatus)row.Status, row.RejectionReason, row.ReviewedBy);
    }

    private async Task<(Guid GameId, Guid PdfId)> SeedGameWithPdfAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = Guid.NewGuid();
        db.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = gameId,
            Title = $"Stale Draft Test Game {Guid.NewGuid():N}",
            Description = "Integration test rulebook",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            YearPublished = 2024,
            MinAge = 12,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _adminId
        });

        var pdfId = Guid.NewGuid();
        db.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = "rulebook.pdf",
            FilePath = $"/tmp/tests/{pdfId}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = _adminId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            Language = "en",
            IsActiveForRag = true,
            LicenseType = 0,
            DocumentCategory = "Rulebook"
        });

        await db.SaveChangesAsync();
        return (gameId, pdfId);
    }

    // Uno scope suo, quindi un DbContext e una connessione diversi da quelli del comando.
    private async Task<Guid> InsertAnalysisAsync(
        Guid gameId,
        Guid pdfId,
        MechanicAnalysisStatus status,
        DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var reviewed = status is MechanicAnalysisStatus.InReview
            or MechanicAnalysisStatus.Published
            or MechanicAnalysisStatus.Rejected;

        var id = Guid.NewGuid();
        db.Set<MechanicAnalysisEntity>().Add(new MechanicAnalysisEntity
        {
            Id = id,
            SharedGameId = gameId,
            PdfDocumentId = pdfId,
            PromptVersion = Prompt,
            Status = (int)status,
            CreatedBy = _adminId,
            CreatedAt = createdAt,
            ReviewedBy = reviewed ? _adminId : null,
            ReviewedAt = reviewed ? createdAt : null,
            RejectionReason = status == MechanicAnalysisStatus.Rejected ? "Rifiutata per il test" : null,
            TotalTokensUsed = 0,
            EstimatedCostUsd = 0m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m
        });

        await db.SaveChangesAsync();
        return id;
    }
}
