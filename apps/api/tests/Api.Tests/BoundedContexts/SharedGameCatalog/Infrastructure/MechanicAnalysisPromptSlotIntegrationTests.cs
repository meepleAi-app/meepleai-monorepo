using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// La fixture di <see cref="MechanicAnalysisPromptSlotIntegrationTests"/>: host condiviso per la
/// classe, database fresco per test (#4050).
/// </summary>
/// <remarks>
/// I doppi hanno stato e vivono quanto la classe: la classe di test li azzera in
/// <c>InitializeAsync</c>, dopo <c>BeginTestAsync</c>.
/// </remarks>
public sealed class MechanicAnalysisPromptSlotHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "mech_slot_4144")
{
    /// <summary>Il pipeline LLM non parte mai: l'accodamento viene solo registrato.</summary>
    internal Mock<IBackgroundTaskService> BackgroundTasks { get; } = CreateBackgroundTaskMock();

    /// <summary>
    /// Generazione: parte dentro la stima del costo, che il handler chiama DOPO il controllo di
    /// idempotenza e PRIMA dell'INSERT.
    /// </summary>
    public ArmedStep BeforeCostEstimate { get; } = new();

    /// <summary>
    /// Rimessa in revisione: parte nel <c>SaveChangesAsync</c> che aggiorna un'analisi, cioè DOPO il
    /// controllo sullo slot e PRIMA dell'UPDATE.
    /// </summary>
    public ArmedStep BeforeAnalysisUpdateSave { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBackgroundTaskService>();
            services.AddSingleton(BackgroundTasks.Object);

            services.RemoveAll<IAnalysisCostEstimator>();
            services.AddScoped<IAnalysisCostEstimator>(_ =>
                new HookedCostEstimator(new AnalysisCostEstimator(), BeforeCostEstimate));

            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<EfCoreUnitOfWork>();
            services.AddScoped<IUnitOfWork>(sp => new HookedUnitOfWork(
                sp.GetRequiredService<EfCoreUnitOfWork>(),
                sp.GetRequiredService<MeepleAiDbContext>(),
                BeforeAnalysisUpdateSave));
        }));

    private static Mock<IBackgroundTaskService> CreateBackgroundTaskMock()
    {
        var mock = new Mock<IBackgroundTaskService>();
        mock.Setup(b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()));
        return mock;
    }
}

/// <summary>
/// Un passo che parte una sola volta, al prossimo punto di aggancio: la riga di una richiesta
/// concorrente che vince la corsa sull'indice unico, oppure un tripwire che prova che la richiesta
/// si è fermata prima.
/// </summary>
public sealed class ArmedStep
{
    private Func<Task>? _pending;

    public void Arm(Func<Task> step) => _pending = step;

    public void ArmTripwire(string reason) =>
        _pending = () => throw new InvalidOperationException($"Tripwire: {reason}");

    public void Reset() => _pending = null;

    internal async Task RunIfArmedAsync()
    {
        var pending = Interlocked.Exchange(ref _pending, null);
        if (pending is not null)
        {
            await pending();
        }
    }

    // Estimate e' sincrono; nell'host di test non c'e' SynchronizationContext, quindi aspettare qui
    // non puo' andare in stallo.
    internal void RunIfArmed() => RunIfArmedAsync().GetAwaiter().GetResult();
}

internal sealed class HookedCostEstimator(IAnalysisCostEstimator inner, ArmedStep hook) : IAnalysisCostEstimator
{
    public AnalysisCostEstimate Estimate(AnalysisCostEstimateInput input)
    {
        hook.RunIfArmed();
        return inner.Estimate(input);
    }
}

internal sealed class HookedUnitOfWork(EfCoreUnitOfWork inner, MeepleAiDbContext db, ArmedStep beforeAnalysisUpdate)
    : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Solo il salvataggio che aggiorna un'analisi: gli altri (sessione, seed) passano intatti.
        if (db.ChangeTracker.Entries<MechanicAnalysisEntity>().Any(e => e.State == EntityState.Modified))
        {
            await beforeAnalysisUpdate.RunIfArmedAsync();
        }

        return await inner.SaveChangesAsync(cancellationToken);
    }

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default) =>
        inner.ExecuteInTransactionAsync(work, cancellationToken);

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default) =>
        inner.ExecuteInTransactionAsync(work, cancellationToken);

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        inner.BeginTransactionAsync(cancellationToken);

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default) =>
        inner.CommitTransactionAsync(cancellationToken);

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) =>
        inner.RollbackTransactionAsync(cancellationToken);

    public void Dispose() => inner.Dispose();
}

/// <summary>
/// L'indice unico filtrato <c>ux_mechanic_analyses_shared_game_pdf_prompt</c> ammette una sola
/// analisi con <c>status &lt;&gt; Rejected</c> per (gioco, PDF, prompt). Due scritture possono metterne
/// dentro una seconda, e prima rispondevano 500 (23505):
/// <list type="bullet">
///   <item>#4144 — l'azione Regenerate (<c>ForceRegenerate: true</c> su <c>POST /admin/mechanic-analyses</c>);</item>
///   <item>la rimessa in revisione di un'analisi Rejected (<c>POST /{id}/submit-review</c>), mentre un'altra
///     analisi ha preso lo slot — lo scenario che il 409 di Regenerate («rifiutala prima») rende comune.</item>
/// </list>
/// </summary>
/// <remarks>
/// Serve Postgres vero: con un repository finto il 23505 non esiste, ed e' il motivo per cui i test
/// unit non vedevano il 500.
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisPromptSlotIntegrationTests
    : IClassFixture<MechanicAnalysisPromptSlotHostFixture>, IAsyncLifetime
{
    private const string EndpointBase = "/api/v1/admin/mechanic-analyses";

    private static readonly Guid TestAdminId = Guid.NewGuid();

    // Data fissa, cosi' il messaggio del Draft (che la cita) e' verificabile senza corse sul minuto.
    private static readonly DateTime SeededCreatedAt = new(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly MechanicAnalysisPromptSlotHostFixture _host;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminToken = null!;
    private string _currentPrompt = null!;

    public MechanicAnalysisPromptSlotIntegrationTests(MechanicAnalysisPromptSlotHostFixture host)
    {
        _host = host;
    }

    public async ValueTask InitializeAsync()
    {
        await _host.BeginTestAsync();
        _host.BackgroundTasks.Invocations.Clear();
        _host.BeforeCostEstimate.Reset();
        _host.BeforeAnalysisUpdateSave.Reset();
        _factory = _host.Factory;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            // #4164: senza prezzo nel catalogo modelli l'estrattore risponde 409 prima dell'INSERT.
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SeedAiModelsCommand());
            (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, TestAdminId);
            _currentPrompt = scope.ServiceProvider.GetRequiredService<IMechanicPromptProvider>().PromptVersion;
        }

        _client = _factory.CreateClient();
    }

    // Host e database appartengono alla fixture; il client e' di questa istanza.
    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Regenerate (#4144) ──────────────────────────────────────────────────

    [Theory]
    [InlineData(MechanicAnalysisStatus.Draft, "in generazione dal 2026-10-01 08:30 UTC")]
    [InlineData(MechanicAnalysisStatus.InReview, "in revisione")]
    [InlineData(MechanicAnalysisStatus.PartiallyExtracted, "parzialmente estratta")]
    [InlineData(MechanicAnalysisStatus.Published, "pubblicata")]
    public async Task Regenerate_WithAnalysisForCurrentPromptInsideTheUniqueIndex_StopsAtTheGuardWith409(
        MechanicAnalysisStatus existingStatus,
        string expectedHint)
    {
        // PDF collegato e indicizzato: senza la guardia la richiesta arriva fino all'INSERT.
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var existingId = await SeedAnalysisAsync(gameId, pdfId, existingStatus);

        // Il catch del 23505 darebbe lo stesso 409: il tripwire prova che la richiesta si ferma al
        // controllo di idempotenza, prima della stima del costo e dell'INSERT.
        _host.BeforeCostEstimate.ArmTripwire("la guardia non ha fermato la richiesta prima della stima");

        var response = await SendGenerateAsync(gameId, pdfId, forceRegenerate: true);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain(expectedHint);
        body.Should().Contain(existingId.ToString(), "il messaggio dice quale analisi blocca la rigenerazione");

        (await CountAnalysesAsync(gameId)).Should().Be(1, "nessuna nuova analisi viene creata");
        VerifyPipelineEnqueued(Times.Never());
    }

    [Fact]
    public async Task Regenerate_WithADraftForCurrentPrompt_SaysItMayBeOrphaned()
    {
        // Un Draft lasciato da un riavvio non finisce mai: il messaggio deve dirlo, non solo «aspetta».
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        await SeedAnalysisAsync(gameId, pdfId, MechanicAnalysisStatus.Draft);

        var response = await SendGenerateAsync(gameId, pdfId, forceRegenerate: true);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("orfana").And.Contain("segnalala");
    }

    [Fact]
    public async Task Regenerate_AfterTheAnalysisForCurrentPromptWasRejected_Returns202WithANewAnalysis()
    {
        // Il consiglio del 409 («rifiutala prima») deve funzionare: Rejected sta fuori dall'indice.
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var rejectedId = await SeedAnalysisAsync(gameId, pdfId, MechanicAnalysisStatus.Rejected);

        var response = await SendGenerateAsync(gameId, pdfId, forceRegenerate: true);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Accepted, body);
        var dto = JsonSerializer.Deserialize<MechanicAnalysisGenerationResponseDto>(body, JsonOptions);
        dto.Should().NotBeNull();
        dto!.IsExistingAnalysis.Should().BeFalse();
        dto.Id.Should().NotBe(rejectedId);
        dto.Status.Should().Be(MechanicAnalysisStatus.Draft);

        (await CountAnalysesAsync(gameId)).Should().Be(2);
        VerifyPipelineEnqueued(Times.Once());
    }

    [Fact]
    public async Task Generate_WhenAConcurrentRequestInsertsFirst_ReturnsTheWinnerAs202()
    {
        // La corsa fra due richieste: entrambe passano il controllo di idempotenza, una sola INSERT
        // passa l'indice. La perdente riceve 23505, che e' una DbUpdateException e NON una
        // DbUpdateConcurrencyException: deve comunque restituire la vincitrice, come una richiesta
        // arrivata un istante dopo.
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var winnerId = Guid.NewGuid();
        _host.BeforeCostEstimate.Arm(() => InsertAnalysisAsync(winnerId, gameId, pdfId, MechanicAnalysisStatus.Draft));

        var response = await SendGenerateAsync(gameId, pdfId, forceRegenerate: false);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Accepted, body);
        var dto = JsonSerializer.Deserialize<MechanicAnalysisGenerationResponseDto>(body, JsonOptions);
        dto.Should().NotBeNull();
        dto!.IsExistingAnalysis.Should().BeTrue();
        dto.Id.Should().Be(winnerId);

        (await CountAnalysesAsync(gameId)).Should().Be(1);
        VerifyPipelineEnqueued(Times.Never());
    }

    [Fact]
    public async Task Regenerate_WhenAConcurrentRequestInsertsFirst_Returns409()
    {
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var winnerId = Guid.NewGuid();
        _host.BeforeCostEstimate.Arm(() => InsertAnalysisAsync(winnerId, gameId, pdfId, MechanicAnalysisStatus.Draft));

        var response = await SendGenerateAsync(gameId, pdfId, forceRegenerate: true);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("in generazione dal");
        body.Should().Contain(winnerId.ToString());

        (await CountAnalysesAsync(gameId)).Should().Be(1);
        VerifyPipelineEnqueued(Times.Never());
    }

    [Fact]
    public async Task Generate_AfterLosingTheRace_LeavesTheScopeCleanForLaterSaves()
    {
        // Il handler gira anche dentro scope altrui (Requeue lo invoca via IMediator): dopo il 23505
        // l'analisi perdente non deve restare Added nel change tracker, ne' i suoi eventi di dominio
        // nel collector, o il prossimo SaveChanges dello stesso scope ritenterebbe l'INSERT.
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var winnerId = Guid.NewGuid();
        _host.BeforeCostEstimate.Arm(() => InsertAnalysisAsync(winnerId, gameId, pdfId, MechanicAnalysisStatus.Draft));

        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // L'override del tetto solleva un evento di dominio: senza pulizia resterebbe nel collector.
        var result = await mediator.Send(new GenerateMechanicAnalysisCommand(
            gameId,
            pdfId,
            TestAdminId,
            CostCapUsd: 1.00m,
            CostCapOverride: new CostCapOverrideInput(2.00m, "Override per il test della corsa persa (#4144).")));

        result.IsExistingAnalysis.Should().BeTrue();
        result.Id.Should().Be(winnerId);

        // Con il change tracker sporco questo SaveChanges rifarebbe l'INSERT perdente: 23505.
        var written = await db.SaveChangesAsync();
        written.Should().Be(0, "nello scope non resta niente da scrivere");
        scope.ServiceProvider.GetRequiredService<IDomainEventCollector>().PeekEvents().Should().BeEmpty();
        (await CountAnalysesAsync(gameId)).Should().Be(1);
    }

    // ── Rimessa in revisione di un'analisi Rejected ─────────────────────────

    [Theory]
    [InlineData(MechanicAnalysisStatus.Draft, "in generazione dal 2026-10-01 08:30 UTC")]
    [InlineData(MechanicAnalysisStatus.InReview, "rifiutala prima di rimettere in revisione questa")]
    public async Task ResubmitForReview_RejectedWhileAnotherAnalysisHoldsTheSlot_StopsAtTheGuardWith409(
        MechanicAnalysisStatus holderStatus,
        string expectedHint)
    {
        // Lo scenario: A in revisione → Rigenera → 409 «rifiutala prima» → l'admin rifiuta A e
        // rigenera (nasce B) → torna su A e la rimette in revisione. Rejected → InReview e' una
        // transizione ammessa, ma riporterebbe A dentro l'indice accanto a B.
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var rejectedId = await SeedAnalysisAsync(gameId, pdfId, MechanicAnalysisStatus.Rejected, withClaim: true);
        var holderId = await SeedAnalysisAsync(gameId, pdfId, holderStatus);

        _host.BeforeAnalysisUpdateSave.ArmTripwire("la guardia non ha fermato la richiesta prima dell'UPDATE");

        var response = await SendSubmitForReviewAsync(rejectedId);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain(expectedHint);
        body.Should().Contain(holderId.ToString(), "il messaggio dice quale analisi tiene lo slot");
        (await GetStatusAsync(rejectedId)).Should().Be(MechanicAnalysisStatus.Rejected);
    }

    [Fact]
    public async Task ResubmitForReview_WhenAnotherAnalysisTakesTheSlotFirst_Returns409()
    {
        // La corsa: la guardia non trova nessuno, poi un'altra richiesta crea B prima dell'UPDATE di A.
        var (gameId, pdfId) = await SeedGameWithIndexedPdfAsync();
        var rejectedId = await SeedAnalysisAsync(gameId, pdfId, MechanicAnalysisStatus.Rejected, withClaim: true);
        var winnerId = Guid.NewGuid();
        _host.BeforeAnalysisUpdateSave.Arm(() => InsertAnalysisAsync(winnerId, gameId, pdfId, MechanicAnalysisStatus.Draft));

        var response = await SendSubmitForReviewAsync(rejectedId);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("in generazione dal");
        body.Should().Contain(winnerId.ToString());
        (await GetStatusAsync(rejectedId)).Should().Be(MechanicAnalysisStatus.Rejected);
        (await CountAnalysesAsync(gameId)).Should().Be(2);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendGenerateAsync(Guid gameId, Guid pdfId, bool forceRegenerate)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            EndpointBase,
            _adminToken,
            new { sharedGameId = gameId, pdfDocumentId = pdfId, costCapUsd = 1.00m, forceRegenerate });
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendSubmitForReviewAsync(Guid analysisId)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{EndpointBase}/{analysisId}/submit-review",
            _adminToken,
            new { });
        return await _client.SendAsync(request);
    }

    private void VerifyPipelineEnqueued(Times times) =>
        _host.BackgroundTasks.Verify(
            b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            times);

    private async Task<int> CountAnalysesAsync(Guid gameId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.MechanicAnalyses.AsNoTracking().IgnoreQueryFilters().CountAsync(a => a.SharedGameId == gameId);
    }

    private async Task<MechanicAnalysisStatus> GetStatusAsync(Guid analysisId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var status = await db.MechanicAnalyses.AsNoTracking().IgnoreQueryFilters()
            .Where(a => a.Id == analysisId)
            .Select(a => a.Status)
            .SingleAsync();
        return (MechanicAnalysisStatus)status;
    }

    private async Task<(Guid GameId, Guid PdfId)> SeedGameWithIndexedPdfAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = Guid.NewGuid();
        db.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = gameId,
            Title = $"Prompt Slot Test Game {Guid.NewGuid():N}",
            Description = "Integration test rulebook",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            YearPublished = 2024,
            MinAge = 12,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestAdminId
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
            UploadedByUserId = TestAdminId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            Language = "en",
            IsActiveForRag = true,
            LicenseType = 0,
            DocumentCategory = "Rulebook"
        });

        for (var i = 0; i < 2; i++)
        {
            db.Set<TextChunkEntity>().Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                Content = $"Rulebook chunk {i}: each turn consists of draw, action, resolve and cleanup phases.",
                ChunkIndex = i,
                PageNumber = i + 1,
                CharacterCount = 90,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.Set<SharedGameDocumentEntity>().Add(new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            PdfDocumentId = pdfId,
            DocumentType = 0, // Rulebook
            Version = "1.0",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestAdminId,
            ApprovalStatus = 1 // Approved
        });

        await db.SaveChangesAsync();
        return (gameId, pdfId);
    }

    private async Task<Guid> SeedAnalysisAsync(
        Guid gameId,
        Guid pdfId,
        MechanicAnalysisStatus status,
        bool withClaim = false)
    {
        var id = Guid.NewGuid();
        await InsertAnalysisAsync(id, gameId, pdfId, status, SeededCreatedAt, withClaim);
        return id;
    }

    // Uno scope suo, quindi un DbContext e una connessione diversi da quelli della richiesta: la riga
    // e' committata quando l'handler fa la sua scrittura, come quella di un altro amministratore.
    private async Task InsertAnalysisAsync(
        Guid id,
        Guid gameId,
        Guid pdfId,
        MechanicAnalysisStatus status,
        DateTime? createdAt = null,
        bool withClaim = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var now = createdAt ?? DateTime.UtcNow;
        var reviewed = status is MechanicAnalysisStatus.InReview
            or MechanicAnalysisStatus.Published
            or MechanicAnalysisStatus.Rejected;

        db.Set<MechanicAnalysisEntity>().Add(new MechanicAnalysisEntity
        {
            Id = id,
            SharedGameId = gameId,
            PdfDocumentId = pdfId,
            PromptVersion = _currentPrompt,
            Status = (int)status,
            CreatedBy = TestAdminId,
            CreatedAt = now,
            ReviewedBy = reviewed ? TestAdminId : null,
            ReviewedAt = reviewed ? now : null,
            RejectionReason = status == MechanicAnalysisStatus.Rejected ? "Rifiutata per il test" : null,
            TotalTokensUsed = 1234,
            EstimatedCostUsd = 0.05m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m
        });

        // La rimessa in revisione richiede almeno un claim (invariante di dominio).
        if (withClaim)
        {
            var claimId = Guid.NewGuid();
            db.Set<MechanicClaimEntity>().Add(new MechanicClaimEntity
            {
                Id = claimId,
                AnalysisId = id,
                Section = 0,
                Text = "Claim: synthetic mechanic description.",
                DisplayOrder = 0,
                Status = 1, // Approved: la rimessa in revisione lo riporta a Pending
                ReviewedBy = TestAdminId,
                ReviewedAt = now
            });
            db.Set<MechanicCitationEntity>().Add(new MechanicCitationEntity
            {
                Id = Guid.NewGuid(),
                ClaimId = claimId,
                PdfPage = 1,
                Quote = "Each turn draw one card.",
                DisplayOrder = 0
            });
        }

        await db.SaveChangesAsync();
    }
}
