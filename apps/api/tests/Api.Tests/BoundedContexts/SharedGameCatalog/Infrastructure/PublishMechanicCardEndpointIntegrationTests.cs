using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for <c>POST /admin/mechanic-analyses/{id}/publish</c> (#527, M1.5). Exercises
/// the full stack against real PostgreSQL (which also validates that the idempotent
/// <c>M1_5_MechanicCardsSchema</c> migration applies): happy path (card + audit + analysis FK persist
/// atomically) and the two common conflicts (F1 not-Published, F2 already-published).
/// </summary>
/// <remarks>
/// The two-analyses active-card race (F4) is structurally prevented for the AI-review path by the
/// pre-existing analysis-level partial unique index (one Published, non-suppressed analysis per game),
/// so it is not reachable via seeding; the card-level index remains as belt-and-braces defense.
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class PublishMechanicCardEndpointIntegrationTests : IAsyncLifetime
{
    private const string EndpointBase = "/api/v1/admin/mechanic-analyses";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminSessionToken = null!;
    private Mock<IBackgroundTaskService> _backgroundTaskMock = null!;

    private static readonly Guid TestAdminId = Guid.NewGuid();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PublishMechanicCardEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"publish_mechanic_card_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _backgroundTaskMock = new Mock<IBackgroundTaskService>();
        _backgroundTaskMock
            .Setup(b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()));

        _factory = IntegrationWebApplicationFactory
            .Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IBackgroundTaskService>();
                    services.AddSingleton<IBackgroundTaskService>(_backgroundTaskMock.Object);
                });
            });

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();

            var (_, token) = await TestSessionHelper.CreateAdminSessionAsync(dbContext, TestAdminId);
            _adminSessionToken = token;
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task Publish_ApprovedAnalysis_Returns201AndPersistsCardAuditAndAnalysisFk()
    {
        var gameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(gameId, status: 2, claimStatuses: new[] { 1 }); // Published + Approved claim

        var response = await SendPublishAsync(analysisId, new { title = "Catan: Game Mechanics" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<PublishMechanicCardResponseDto>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.SharedGameId.Should().Be(gameId);
        dto.OriginAnalysisId.Should().Be(analysisId);
        dto.Version.Should().Be(1);
        dto.Title.Should().Be("Catan: Game Mechanics");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var card = await db.MechanicCards.AsNoTracking().SingleAsync(c => c.Id == dto.CardId);
        card.SharedGameId.Should().Be(gameId);
        card.Origin.Should().Be("ai_reviewed");
        card.Version.Should().Be(1);
        card.IsSuppressed.Should().BeFalse();

        // PostgreSQL reformats jsonb on storage, so parse rather than string-match.
        using (var contentDoc = JsonDocument.Parse(card.Content))
        {
            var contentRoot = contentDoc.RootElement;
            contentRoot.GetProperty("schema_version").GetInt32().Should().Be(3); // spec 2026-10-08: v3 adds kind/priority/overrides/trigger.
            contentRoot.GetProperty("source_analysis_id").GetGuid().Should().Be(analysisId);
            contentRoot.GetProperty("claims").GetArrayLength().Should().Be(1);
        }

        var audit = await db.MechanicCardAuditLog.AsNoTracking().Where(a => a.CardId == dto.CardId).ToListAsync();
        audit.Should().ContainSingle(a => a.Action == "published" && a.ActorId == TestAdminId);

        var analysis = await db.MechanicAnalyses.AsNoTracking().IgnoreQueryFilters().SingleAsync(a => a.Id == analysisId);
        analysis.PublishedCardId.Should().Be(dto.CardId);
    }

    [Fact]
    public async Task Publish_InReviewAnalysis_Returns409()
    {
        var gameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(gameId, status: 1, claimStatuses: new[] { 1 }); // InReview

        var response = await SendPublishAsync(analysisId, new { });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Publish_AlreadyPublishedAnalysis_Returns409()
    {
        var gameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(gameId, status: 2, claimStatuses: new[] { 1 });

        var first = await SendPublishAsync(analysisId, new { });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await SendPublishAsync(analysisId, new { });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Requeue_WithPartiallyExtractedAnalysisForCurrentPrompt_Returns409()
    {
        // ux_mechanic_analyses_shared_game_pdf_prompt is unique with filter `status <> 3`, so a
        // PartiallyExtracted row (status 4) blocks a second row for the same (game, pdf, prompt).
        // The PDF is linked and indexed so that, without the requeue guard, the request reaches the
        // INSERT of the new analysis (23505 → 500) instead of stopping at an earlier 404/409.
        var gameId = await SeedSharedGameAsync();
        var pdfId = await SeedIndexedPdfLinkedToGameAsync(gameId);
        var publishedId = await SeedAnalysisAsync(gameId, status: 2, claimStatuses: new[] { 1 }, pdfDocumentId: pdfId);
        (await SendPublishAsync(publishedId, new { })).StatusCode.Should().Be(HttpStatusCode.Created);

        string currentPrompt;
        using (var scope = _factory.Services.CreateScope())
        {
            currentPrompt = scope.ServiceProvider.GetRequiredService<IMechanicPromptProvider>().PromptVersion;
        }

        currentPrompt.Should().NotBe("mechanic-extractor-v1", "the published analysis must not occupy the current prompt slot");
        await SeedAnalysisAsync(gameId, status: 4, claimStatuses: new[] { 0 }, pdfDocumentId: pdfId, promptVersion: currentPrompt);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{EndpointBase}/requeue/{gameId}",
            _adminSessionToken,
            new { });
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("parzialmente estratta");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (await db.MechanicAnalyses.AsNoTracking().IgnoreQueryFilters().CountAsync(a => a.SharedGameId == gameId))
            .Should().Be(2, "no new analysis is created");
        _backgroundTaskMock.Verify(
            b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            Times.Never);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendPublishAsync(Guid analysisId, object body)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{EndpointBase}/{analysisId}/publish",
            _adminSessionToken,
            body);
        return await _client.SendAsync(request);
    }

    private async Task<Guid> SeedSharedGameAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = Guid.NewGuid();
        dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = gameId,
            Title = $"Publish Test Game {Guid.NewGuid():N}",
            Description = "Integration test rulebook",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            YearPublished = 2024,
            MinAge = 12,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestAdminId
        });
        await dbContext.SaveChangesAsync();
        return gameId;
    }

    // Same shape as MechanicAnalysisEndpointsIntegrationTests: a Ready PDF with text chunks, linked to
    // the game through shared_game_documents, so GenerateMechanicAnalysisCommandHandler passes its
    // 404 (link) and 409 (no chunks) checks.
    private async Task<Guid> SeedIndexedPdfLinkedToGameAsync(Guid sharedGameId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var pdfId = Guid.NewGuid();
        dbContext.Set<PdfDocumentEntity>().Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = sharedGameId,
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
            dbContext.Set<TextChunkEntity>().Add(new TextChunkEntity
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

        dbContext.Set<SharedGameDocumentEntity>().Add(new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfId,
            DocumentType = 0, // Rulebook
            Version = "1.0",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestAdminId,
            ApprovalStatus = 1 // Approved
        });

        await dbContext.SaveChangesAsync();
        return pdfId;
    }

    private async Task<Guid> SeedAnalysisAsync(
        Guid sharedGameId,
        int status,
        int[] claimStatuses,
        Guid? pdfDocumentId = null,
        string promptVersion = "mechanic-extractor-v1")
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var analysisId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        dbContext.Set<MechanicAnalysisEntity>().Add(new MechanicAnalysisEntity
        {
            Id = analysisId,
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfDocumentId ?? Guid.NewGuid(),
            PromptVersion = promptVersion,
            Status = status,
            CreatedBy = TestAdminId,
            CreatedAt = now,
            ReviewedBy = status is 1 or 2 ? TestAdminId : null,
            ReviewedAt = status is 1 or 2 ? now : null,
            TotalTokensUsed = 1234,
            EstimatedCostUsd = 0.05m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m
        });

        for (var i = 0; i < claimStatuses.Length; i++)
        {
            var claimId = Guid.NewGuid();
            dbContext.Set<MechanicClaimEntity>().Add(new MechanicClaimEntity
            {
                Id = claimId,
                AnalysisId = analysisId,
                Section = i % 6,
                Text = $"Claim {i}: synthetic mechanic description.",
                DisplayOrder = i,
                Status = claimStatuses[i],
                ReviewedBy = claimStatuses[i] == 0 ? null : TestAdminId,
                ReviewedAt = claimStatuses[i] == 0 ? null : now
            });
            dbContext.Set<MechanicCitationEntity>().Add(new MechanicCitationEntity
            {
                Id = Guid.NewGuid(),
                ClaimId = claimId,
                PdfPage = 1,
                Quote = "Each turn draw one card.",
                DisplayOrder = 0
            });
        }

        await dbContext.SaveChangesAsync();
        return analysisId;
    }
}
