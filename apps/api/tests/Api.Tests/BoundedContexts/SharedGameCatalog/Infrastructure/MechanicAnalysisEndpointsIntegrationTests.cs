using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
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
/// Integration tests for M1.2 Mechanic Extractor admin endpoints (ISSUE-524 / ADR-051).
/// Covers:
/// <list type="bullet">
///   <item><c>POST /api/v1/admin/mechanic-analyses</c> — async generation pipeline (202 Accepted).</item>
///   <item><c>GET  /api/v1/admin/mechanic-analyses/{id}/status</c> — lifecycle telemetry.</item>
/// </list>
/// Async executor is stubbed via a mocked <see cref="IBackgroundTaskService"/> so the tests can
/// assert the synchronous contract (validation, T7 idempotency, T8 cost gate, 404/409 mapping)
/// without depending on the real LLM pipeline.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisEndpointsIntegrationTests : IAsyncLifetime
{
    private const string EndpointBase = "/api/v1/admin/mechanic-analyses";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminSessionToken = null!;
    private Mock<IBackgroundTaskService> _backgroundTaskMock = null!;

    internal static readonly Guid TestAdminId = Guid.NewGuid();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MechanicAnalysisEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"mechanic_analysis_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _backgroundTaskMock = new Mock<IBackgroundTaskService>();
        // Default: swallow background enqueue so the executor never runs under test.
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

    // ────────────────────────────────────────────────────────────────────────
    // POST /api/v1/admin/mechanic-analyses — happy path & async enqueue
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_HappyPath_Returns202WithStatusUrl()
    {
        // Arrange
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 6);
        var body = new GenerateBody(sharedGameId, pdfDocumentId, CostCapUsd: 1.00m);

        // Act
        var response = await SendGenerateAsync(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var dto = await response.Content.ReadFromJsonAsync<MechanicAnalysisGenerationResponseDto>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.Id.Should().NotBe(Guid.Empty);
        dto.Status.Should().Be(MechanicAnalysisStatus.Draft);
        dto.PromptVersion.Should().NotBeNullOrWhiteSpace();
        dto.CostCapUsd.Should().Be(1.00m);
        dto.EstimatedCostUsd.Should().BeGreaterThan(0m);
        dto.ProjectedTotalTokens.Should().BeGreaterThan(0);
        dto.CostCapOverrideApplied.Should().BeFalse();
        dto.StatusUrl.Should().Be($"{EndpointBase}/{dto.Id}/status");
        dto.IsExistingAnalysis.Should().BeFalse();

        // 202 Accepted body also exposes the StatusUrl via the Location header indirectly.
        _backgroundTaskMock.Verify(
            b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            Times.Once);
    }

    [Fact]
    public async Task Generate_DuplicateRequest_ShortCircuitsWithIsExistingAnalysisTrue()
    {
        // Arrange
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 4);
        var body = new GenerateBody(sharedGameId, pdfDocumentId, CostCapUsd: 1.00m);

        // Act
        var first = await SendGenerateAsync(body);
        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var firstDto = await first.Content.ReadFromJsonAsync<MechanicAnalysisGenerationResponseDto>(JsonOptions);

        var second = await SendGenerateAsync(body);

        // Assert — T7 idempotency on (SharedGameId, PdfDocumentId, PromptVersion).
        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var secondDto = await second.Content.ReadFromJsonAsync<MechanicAnalysisGenerationResponseDto>(JsonOptions);
        secondDto.Should().NotBeNull();
        secondDto!.IsExistingAnalysis.Should().BeTrue();
        secondDto.Id.Should().Be(firstDto!.Id);
        secondDto.PromptVersion.Should().Be(firstDto.PromptVersion);

        // Only the first call enqueues background work; T7 short-circuit MUST NOT re-enqueue.
        _backgroundTaskMock.Verify(
            b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST — 404 mappings (NotFoundException → HTTP 404)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_MissingSharedGame_Returns404()
    {
        // Arrange
        var body = new GenerateBody(Guid.NewGuid(), Guid.NewGuid(), CostCapUsd: 1.00m);

        // Act
        var response = await SendGenerateAsync(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Generate_PdfNotLinkedToGame_Returns404()
    {
        // Arrange — SharedGame exists, PDF exists, but no SharedGameDocument row linking them.
        var sharedGameId = await SeedSharedGameAsync();
        var pdfDocumentId = await SeedPdfDocumentAsync(chunkCount: 3);

        var body = new GenerateBody(sharedGameId, pdfDocumentId, CostCapUsd: 1.00m);

        // Act
        var response = await SendGenerateAsync(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST — 409 mappings (ConflictException → HTTP 409)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_PdfWithoutIndexedChunks_Returns409()
    {
        // Arrange — PDF linked to game, but zero TextChunks.
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 0);
        var body = new GenerateBody(sharedGameId, pdfDocumentId, CostCapUsd: 1.00m);

        // Act
        var response = await SendGenerateAsync(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Generate_ProjectedCostExceedsCap_Returns409()
    {
        // Arrange — valid indexed rulebook, but cap is far below the 6-section projection
        // (system prompt alone is ~700 tokens × 6 sections).
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 6);
        var body = new GenerateBody(sharedGameId, pdfDocumentId, CostCapUsd: 0.000_001m);

        // Act
        var response = await SendGenerateAsync(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Background task MUST NOT be enqueued when planning rejects the run.
        _backgroundTaskMock.Verify(
            b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST — CostCapOverride (B3=A) happy path and validation
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_WithValidCostCapOverride_Returns202WithOverrideApplied()
    {
        // Arrange
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 5);
        var body = new GenerateBody(
            sharedGameId,
            pdfDocumentId,
            CostCapUsd: 0.50m,
            CostCapOverride: new OverrideBody(
                NewCapUsd: 2.00m,
                Reason: "Campaign rulebook requires full 6-section extraction for QA audit."));

        // Act
        var response = await SendGenerateAsync(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var dto = await response.Content.ReadFromJsonAsync<MechanicAnalysisGenerationResponseDto>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.CostCapOverrideApplied.Should().BeTrue();
        // Response DTO reports the EFFECTIVE cap (the override value), not the original submission.
        dto.CostCapUsd.Should().Be(2.00m);
    }

    [Fact]
    public async Task Generate_OverrideNotGreaterThanOriginal_Returns422()
    {
        // Arrange — validator requires NewCapUsd > CostCapUsd.
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 3);
        var body = new GenerateBody(
            sharedGameId,
            pdfDocumentId,
            CostCapUsd: 1.00m,
            CostCapOverride: new OverrideBody(
                NewCapUsd: 1.00m,
                Reason: "Attempting an override equal to the original cap should be rejected."));

        // Act
        var response = await SendGenerateAsync(body);

        // Assert — FluentValidation.ValidationException → 422 (ApiExceptionHandlerMiddleware).
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Generate_OverrideReasonTooShort_Returns422()
    {
        // Arrange — validator requires Reason length >= 20 chars.
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 3);
        var body = new GenerateBody(
            sharedGameId,
            pdfDocumentId,
            CostCapUsd: 0.50m,
            CostCapOverride: new OverrideBody(
                NewCapUsd: 1.00m,
                Reason: "too short"));

        // Act
        var response = await SendGenerateAsync(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST — CostCapUsd validator bounds (0 < cap <= 10)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_NonPositiveCostCapUsd_Returns422()
    {
        // Arrange
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 3);
        var body = new GenerateBody(sharedGameId, pdfDocumentId, CostCapUsd: 0m);

        // Act
        var response = await SendGenerateAsync(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Generate_CostCapUsdAboveMaximum_Returns422()
    {
        // Arrange — MaxCostCapUsd = 10.0 in GenerateMechanicAnalysisCommandValidator.
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 3);
        var body = new GenerateBody(sharedGameId, pdfDocumentId, CostCapUsd: 99.00m);

        // Act
        var response = await SendGenerateAsync(body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST — auth filter (RequireAdminSessionFilter → 401 without cookie)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_WithoutSessionCookie_Returns401()
    {
        // Arrange
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 3);
        var body = new GenerateBody(sharedGameId, pdfDocumentId, CostCapUsd: 1.00m);

        // Act — send WITHOUT the authenticated cookie helper.
        var response = await _client.PostAsJsonAsync(EndpointBase, body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET /api/v1/admin/mechanic-analyses/{id}/status
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_ForExistingDraftAnalysis_Returns200WithEmptySectionRuns()
    {
        // Arrange — run the generate flow first so a Draft row exists.
        var (sharedGameId, pdfDocumentId) = await SeedHappyPathAsync(chunkCount: 4);
        var generateBody = new GenerateBody(sharedGameId, pdfDocumentId, CostCapUsd: 1.00m);
        var generateResponse = await SendGenerateAsync(generateBody);
        generateResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var generated = await generateResponse.Content.ReadFromJsonAsync<MechanicAnalysisGenerationResponseDto>(JsonOptions);
        generated.Should().NotBeNull();

        // Act
        var statusRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{EndpointBase}/{generated!.Id}/status",
            _adminSessionToken);
        var statusResponse = await _client.SendAsync(statusRequest);

        // Assert
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await statusResponse.Content.ReadFromJsonAsync<MechanicAnalysisStatusDto>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(generated.Id);
        dto.SharedGameId.Should().Be(sharedGameId);
        dto.PdfDocumentId.Should().Be(pdfDocumentId);
        dto.Status.Should().Be(MechanicAnalysisStatus.Draft);
        dto.CreatedBy.Should().Be(TestAdminId);
        dto.CostCapUsd.Should().Be(1.00m);
        dto.CostCapOverrideApplied.Should().BeFalse();
        dto.IsSuppressed.Should().BeFalse();
        // Background executor is mocked → no section runs recorded yet.
        dto.SectionRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStatus_ForUnknownAnalysisId_Returns404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{EndpointBase}/{nonExistentId}/status",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendGenerateAsync(GenerateBody body)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            EndpointBase,
            _adminSessionToken,
            body);
        return await _client.SendAsync(request);
    }

    private async Task<(Guid SharedGameId, Guid PdfDocumentId)> SeedHappyPathAsync(int chunkCount)
    {
        var sharedGameId = await SeedSharedGameAsync();
        var pdfDocumentId = await SeedPdfDocumentAsync(chunkCount);
        await SeedSharedGameDocumentLinkAsync(sharedGameId, pdfDocumentId);
        return (sharedGameId, pdfDocumentId);
    }

    private async Task<Guid> SeedSharedGameAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = Guid.NewGuid();
        var game = new SharedGameEntity
        {
            Id = gameId,
            Title = $"Test Game {Guid.NewGuid():N}",
            Description = "Integration test rulebook",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            YearPublished = 2024,
            MinAge = 12,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestAdminId
        };
        dbContext.Set<SharedGameEntity>().Add(game);
        await dbContext.SaveChangesAsync();
        return gameId;
    }

    private async Task<Guid> SeedPdfDocumentAsync(int chunkCount)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var pdfId = Guid.NewGuid();
        var pdf = new PdfDocumentEntity
        {
            Id = pdfId,
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
        };
        dbContext.Set<PdfDocumentEntity>().Add(pdf);

        for (int i = 0; i < chunkCount; i++)
        {
            var chunk = new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                Content = $"Rulebook chunk {i}: the game proceeds through setup, play rounds, and scoring. "
                    + "Each turn consists of draw, action, resolve, and cleanup phases. Victory is determined by "
                    + "the player with the highest score after the final round.",
                ChunkIndex = i,
                PageNumber = i + 1,
                CharacterCount = 256,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Set<TextChunkEntity>().Add(chunk);
        }

        await dbContext.SaveChangesAsync();
        return pdfId;
    }

    private async Task SeedSharedGameDocumentLinkAsync(Guid sharedGameId, Guid pdfDocumentId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var link = new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfDocumentId,
            DocumentType = 0, // Rulebook
            Version = "1.0",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestAdminId,
            ApprovalStatus = 1 // Approved
        };
        dbContext.Set<SharedGameDocumentEntity>().Add(link);
        await dbContext.SaveChangesAsync();
    }

    // DTO shapes mirroring AdminMechanicAnalysesEndpoints request records (internal to Api).
    private sealed record GenerateBody(
        Guid SharedGameId,
        Guid PdfDocumentId,
        decimal CostCapUsd,
        OverrideBody? CostCapOverride = null);

    private sealed record OverrideBody(decimal NewCapUsd, string Reason);
}
