using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Endpoints;

/// <summary>
/// Integration tests for the AI Comprehension Validation admin endpoints
/// (ADR-051 Sprint 1 / Task 32). Covers golden CRUD, BGG tag import, metrics
/// calculation, certification override, recalc-all, dashboard, trend, and
/// threshold management routes. Asserts auth enforcement (401 without session),
/// happy-path status codes (200 / 201 / 204), and exception → HTTP mapping
/// (404 / 409 / 400).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AdminMechanicExtractorValidationEndpointsTests : IAsyncLifetime
{
    private const string EndpointBase = "/api/v1/admin/mechanic-extractor";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminSessionToken = null!;

    private static readonly Guid TestAdminId = Guid.NewGuid();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AdminMechanicExtractorValidationEndpointsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"mechanic_validation_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

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

    // ────────────────────────────────────────────────────────────────────
    // Auth enforcement: every route must reject anonymous calls (401)
    // ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET", "/golden/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/golden/00000000-0000-0000-0000-000000000001/version-hash")]
    [InlineData("POST", "/golden")]
    [InlineData("PUT", "/golden/00000000-0000-0000-0000-000000000002")]
    [InlineData("DELETE", "/golden/00000000-0000-0000-0000-000000000002")]
    [InlineData("POST", "/golden/00000000-0000-0000-0000-000000000001/bgg-tags")]
    [InlineData("POST", "/analyses/00000000-0000-0000-0000-000000000003/metrics")]
    [InlineData("POST", "/analyses/00000000-0000-0000-0000-000000000003/override-certification")]
    [InlineData("POST", "/metrics/recalculate-all")]
    [InlineData("GET", "/metrics/recalc-jobs/00000000-0000-0000-0000-000000000004")]
    [InlineData("POST", "/metrics/recalc-jobs/00000000-0000-0000-0000-000000000004/cancel")]
    [InlineData("GET", "/dashboard")]
    [InlineData("GET", "/dashboard/00000000-0000-0000-0000-000000000001/trend")]
    [InlineData("GET", "/thresholds")]
    [InlineData("PUT", "/thresholds")]
    public async Task EveryEndpoint_WithoutSession_Returns401(string method, string relative)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), $"{EndpointBase}{relative}");
        // Provide a body for POST/PUT to avoid 400-on-missing-body short-circuiting before auth.
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /golden/{sharedGameId}
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGolden_UnknownGame_ReturnsEmptyDto200()
    {
        // Handler doesn't 404 on missing game; returns empty claims/tags collection.
        var response = await SendAsync(HttpMethod.Get, $"/golden/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /golden/{sharedGameId}/version-hash
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGoldenVersionHash_UnknownGame_ReturnsEmptyString200()
    {
        var response = await SendAsync(HttpMethod.Get, $"/golden/{Guid.NewGuid()}/version-hash");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────────────
    // POST /golden — create
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGolden_HappyPath_Returns201()
    {
        var sharedGameId = await SeedSharedGameAsync();

        var body = new
        {
            sharedGameId,
            section = MechanicSection.Mechanics,
            statement = "On a player's turn, that player draws one card from the deck.",
            expectedPage = 4,
            sourceQuote = "On your turn, draw a card from the top of the deck.",
        };

        var response = await SendAsync(HttpMethod.Post, "/golden", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateGolden_InvalidPayload_Returns400Family()
    {
        var sharedGameId = await SeedSharedGameAsync();

        var body = new
        {
            sharedGameId,
            section = MechanicSection.Mechanics,
            statement = "", // empty → fails 1..500 length validator
            expectedPage = 0, // < 1 → fails validator
            sourceQuote = "",
        };

        var response = await SendAsync(HttpMethod.Post, "/golden", body);

        // FluentValidation surfaces as 400 BadRequest or 422 UnprocessableEntity
        // depending on the configured behavior; both are accepted as "validation failed".
        response.StatusCode.Should().Match(s =>
            s == HttpStatusCode.BadRequest || s == HttpStatusCode.UnprocessableEntity);
    }

    // ────────────────────────────────────────────────────────────────────
    // PUT /golden/{id}
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateGolden_HappyPath_Returns204()
    {
        var sharedGameId = await SeedSharedGameAsync();
        var claimId = await SeedGoldenClaimAsync(sharedGameId);

        var body = new
        {
            statement = "Updated statement after curator review.",
            expectedPage = 12,
            sourceQuote = "Updated source quote from the rulebook.",
        };

        var response = await SendAsync(HttpMethod.Put, $"/golden/{claimId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateGolden_UnknownClaimId_Returns404()
    {
        var body = new
        {
            statement = "Updated statement after curator review.",
            expectedPage = 12,
            sourceQuote = "Updated source quote from the rulebook.",
        };

        var response = await SendAsync(HttpMethod.Put, $"/golden/{Guid.NewGuid()}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────
    // DELETE /golden/{id}
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteGolden_HappyPath_Returns204()
    {
        var sharedGameId = await SeedSharedGameAsync();
        var claimId = await SeedGoldenClaimAsync(sharedGameId);

        var response = await SendAsync(HttpMethod.Delete, $"/golden/{claimId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteGolden_AlreadyDeactivated_EfQueryFilterReturns404()
    {
        var sharedGameId = await SeedSharedGameAsync();
        var claimId = await SeedGoldenClaimAsync(sharedGameId, isDeleted: true);

        var response = await SendAsync(HttpMethod.Delete, $"/golden/{claimId}");

        // Soft-deleted rows are filtered out by EF query filter → repository returns null → 404.
        // (The 409 path requires the claim to be loaded but already deactivated, which the
        // global query filter prevents from happening for already-soft-deleted rows.)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGolden_UnknownClaimId_Returns404()
    {
        var response = await SendAsync(HttpMethod.Delete, $"/golden/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────
    // POST /golden/{sharedGameId}/bgg-tags
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportBggTags_HappyPath_Returns200()
    {
        var sharedGameId = await SeedSharedGameAsync();

        var body = new
        {
            tags = new[]
            {
                new { name = "Worker Placement", category = "mechanic" },
                new { name = "Eurogame", category = "theme" },
            },
        };

        var response = await SendAsync(
            HttpMethod.Post,
            $"/golden/{sharedGameId}/bgg-tags",
            body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────────────
    // POST /analyses/{id}/metrics
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CalculateMetrics_UnknownAnalysisId_Returns404()
    {
        var response = await SendAsync(HttpMethod.Post, $"/analyses/{Guid.NewGuid()}/metrics");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CalculateMetrics_AnalysisNotPublished_Returns409()
    {
        // Seed a Draft analysis (status=0) — handler requires Published (status=2) → 409.
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 0);

        var response = await SendAsync(HttpMethod.Post, $"/analyses/{analysisId}/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ────────────────────────────────────────────────────────────────────
    // POST /analyses/{id}/override-certification
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OverrideCertification_UnknownAnalysisId_Returns404()
    {
        var body = new
        {
            reason = "Curator manually verified that mechanic coverage meets domain standards.",
        };

        var response = await SendAsync(
            HttpMethod.Post,
            $"/analyses/{Guid.NewGuid()}/override-certification",
            body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OverrideCertification_NoPriorMetrics_Returns409()
    {
        // Seed a Published analysis with LastMetricsId=null — handler returns 409
        // ("no prior metrics; cannot override certification").
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 2, lastMetricsId: null);

        var body = new
        {
            reason = "Curator manually verified that mechanic coverage meets domain standards.",
        };

        var response = await SendAsync(
            HttpMethod.Post,
            $"/analyses/{analysisId}/override-certification",
            body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ────────────────────────────────────────────────────────────────────
    // POST /metrics/recalculate-all (Sprint 2 / Task 10 — async upgrade)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecalculateAll_Enqueue_Returns202WithLocationHeaderAndJobId()
    {
        // Sprint 2 changed this from synchronous (200 OK) to async (202 Accepted).
        // The handler persists a Pending MechanicRecalcJob and returns the id;
        // the background worker (not exercised here) is what actually iterates analyses.
        var response = await SendAsync(HttpMethod.Post, "/metrics/recalculate-all");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Location header must point at the GET status endpoint.
        response.Headers.Location.Should().NotBeNull();
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith("/api/v1/admin/mechanic-extractor/metrics/recalc-jobs/");

        // Body should carry the JobId (case-insensitive JSON).
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions, TestContext.Current.CancellationToken);
        body.TryGetProperty("jobId", out var jobIdProp).Should().BeTrue();
        var jobId = jobIdProp.GetGuid();
        jobId.Should().NotBeEmpty();
        location.Should().EndWith(jobId.ToString());

        // Aggregate must exist and be Pending (worker has not run yet).
        using var scope = _factory.Services.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IMechanicRecalcJobRepository>();
        var persisted = await jobRepo.GetByIdAsync(jobId, TestContext.Current.CancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(RecalcJobStatus.Pending);
        persisted.TriggeredByUserId.Should().Be(TestAdminId);
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /metrics/recalc-jobs/{id}
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecalcJobStatus_ExistingJob_Returns200WithDto()
    {
        // Enqueue, then read it back through the public surface.
        var enqueueResponse = await SendAsync(HttpMethod.Post, "/metrics/recalculate-all");
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueueBody = await enqueueResponse.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions, TestContext.Current.CancellationToken);
        var jobId = enqueueBody.GetProperty("jobId").GetGuid();

        var response = await SendAsync(HttpMethod.Get, $"/metrics/recalc-jobs/{jobId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<RecalcJobStatusDto>(
            JsonOptions, TestContext.Current.CancellationToken);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(jobId);
        dto.Status.Should().Be(RecalcJobStatus.Pending);
        dto.TriggeredByUserId.Should().Be(TestAdminId);
        dto.EtaSeconds.Should().BeNull("ETA is only computed for Running jobs with progress");
    }

    [Fact]
    public async Task GetRecalcJobStatus_UnknownId_Returns404()
    {
        var response = await SendAsync(HttpMethod.Get, $"/metrics/recalc-jobs/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────
    // POST /metrics/recalc-jobs/{id}/cancel
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelRecalcJob_PendingJob_Returns204AndSetsCancellationFlag()
    {
        // Enqueue first, then cancel. Aggregate must surface CancellationRequested=true.
        var enqueueResponse = await SendAsync(HttpMethod.Post, "/metrics/recalculate-all");
        enqueueResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var enqueueBody = await enqueueResponse.Content.ReadFromJsonAsync<JsonElement>(
            JsonOptions, TestContext.Current.CancellationToken);
        var jobId = enqueueBody.GetProperty("jobId").GetGuid();

        var response = await SendAsync(HttpMethod.Post, $"/metrics/recalc-jobs/{jobId}/cancel");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IMechanicRecalcJobRepository>();
        var persisted = await jobRepo.GetByIdAsync(jobId, TestContext.Current.CancellationToken);
        persisted.Should().NotBeNull();
        persisted!.CancellationRequested.Should().BeTrue();
        // Cancellation is a flag, not a status — Pending stays Pending until the worker reacts.
        persisted.Status.Should().Be(RecalcJobStatus.Pending);
    }

    [Fact]
    public async Task CancelRecalcJob_UnknownId_Returns404()
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"/metrics/recalc-jobs/{Guid.NewGuid()}/cancel");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /dashboard
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboard_Empty_Returns200()
    {
        var response = await SendAsync(HttpMethod.Get, "/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /dashboard/{sharedGameId}/trend
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTrend_UnknownGame_ReturnsEmptyList200()
    {
        var response = await SendAsync(
            HttpMethod.Get,
            $"/dashboard/{Guid.NewGuid()}/trend?take=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /thresholds
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetThresholds_Returns200()
    {
        var response = await SendAsync(HttpMethod.Get, "/thresholds");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────────────
    // PUT /thresholds
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateThresholds_HappyPath_Returns204()
    {
        var body = new
        {
            minCoveragePct = 60.0m,
            maxPageTolerance = 2,
            minBggMatchPct = 50.0m,
            minOverallScore = 65.0m,
        };

        var response = await SendAsync(HttpMethod.Put, "/thresholds", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateThresholds_OutOfRange_Returns400Family()
    {
        var body = new
        {
            minCoveragePct = 150.0m, // > 100 → invalid
            maxPageTolerance = -1, // < 0 → invalid
            minBggMatchPct = -10.0m, // < 0 → invalid
            minOverallScore = 200.0m, // > 100 → invalid
        };

        var response = await SendAsync(HttpMethod.Put, "/thresholds", body);

        response.StatusCode.Should().Match(s =>
            s == HttpStatusCode.BadRequest || s == HttpStatusCode.UnprocessableEntity);
    }

    // ────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string relative, object? body = null)
    {
        var request = body is null
            ? TestSessionHelper.CreateAuthenticatedRequest(method, $"{EndpointBase}{relative}", _adminSessionToken)
            : TestSessionHelper.CreateAuthenticatedRequest(method, $"{EndpointBase}{relative}", _adminSessionToken, body);
        return _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedSharedGameAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = Guid.NewGuid();
        var game = new SharedGameEntity
        {
            Id = gameId,
            Title = $"Validation Endpoint Test Game {Guid.NewGuid():N}",
            Description = "Integration test rulebook",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            YearPublished = 2024,
            MinAge = 10,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestAdminId,
        };
        dbContext.Set<SharedGameEntity>().Add(game);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return gameId;
    }

    private async Task<Guid> SeedGoldenClaimAsync(Guid sharedGameId, bool isDeleted = false)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var claimId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var claim = new MechanicGoldenClaimEntity
        {
            Id = claimId,
            SharedGameId = sharedGameId,
            Section = (int)MechanicSection.Mechanics,
            Statement = "Each player begins with five resource tokens.",
            ExpectedPage = 3,
            SourceQuote = "Each player starts the game with five tokens.",
            KeywordsJson = "[]",
            CuratorUserId = TestAdminId,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? now : null,
        };
        dbContext.Set<MechanicGoldenClaimEntity>().Add(claim);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return claimId;
    }

    private async Task<Guid> SeedAnalysisAsync(
        Guid sharedGameId,
        int status,
        Guid? lastMetricsId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var analysisId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var analysis = new MechanicAnalysisEntity
        {
            Id = analysisId,
            SharedGameId = sharedGameId,
            PdfDocumentId = Guid.NewGuid(),
            PromptVersion = "mechanic-extractor-v1",
            Status = status,
            CreatedBy = TestAdminId,
            CreatedAt = now,
            ReviewedBy = status >= 1 ? TestAdminId : null,
            ReviewedAt = status >= 1 ? now : null,
            TotalTokensUsed = 1234,
            EstimatedCostUsd = 0.05m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m,
            LastMetricsId = lastMetricsId,
        };
        dbContext.Set<MechanicAnalysisEntity>().Add(analysis);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return analysisId;
    }
}
