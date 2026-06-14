using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
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

namespace Api.Tests.Integration.Routing;

/// <summary>
/// Issue #1823 Wave 3 M12 — HTTP integration smoke tests for the admin
/// <c>/api/v1/admin/wikidata/enrichment</c> surface. Covers the auth filter
/// chain end-to-end. The full happy-path + force-refresh + outcome propagation
/// matrix is deferred to the M13 admin dead-letter page PR (bundled with the
/// admin UI E2E sweep).
/// </summary>
/// <remarks>
/// Lisa Crispin guard against deferral creep (spec-panel sess.2026-06-11):
/// without this smoke test, the auth filter wiring + route constraint + body
/// deserialization (null → ForceRefresh=false) would have no automated guard
/// in CI until M13 ships. The single 401 anonymous probe verifies the filter
/// chain is wired and the route is reachable.
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public sealed class AdminWikidataCoverEnrichmentEndpointsTests : IAsyncLifetime
{
    private const string EndpointBase = "/api/v1/admin/wikidata/enrichment";
    private static readonly Guid TestGameId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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

    public AdminWikidataCoverEnrichmentEndpointsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"wikidata_enrichment_admin_{Guid.NewGuid():N}";
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
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Trigger_AnonymousRequest_Returns401()
    {
        // Hits the endpoint with no session cookie / auth header. The group-level
        // RequireAdminSessionFilter must short-circuit BEFORE the handler runs.
        var url = $"{EndpointBase}/{TestGameId}";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new { forceRefresh = false }),
        };

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the admin endpoint filter chain must reject anonymous probes BEFORE the MediatR pipeline executes");
    }

    [Fact]
    public async Task Trigger_AnonymousRequest_NoBody_Returns401()
    {
        // Confirms the auth filter fires regardless of body shape — null body
        // (treated as ForceRefresh=false at handler level) must still be 401
        // for an anonymous caller.
        var url = $"{EndpointBase}/{TestGameId}";
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListDeadLetters_AnonymousRequest_Returns401()
    {
        // Wave 3 M13 GET endpoint must also be gated by the group-level
        // RequireAdminSessionFilter, no different from POST.
        var url = $"{EndpointBase}/dead-letters?skip=0&take=10";

        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the dead-letter admin page contains sensitive operational data and MUST require an admin session");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Wave 3 M16 — authenticated admin happy-path coverage.
    //
    // These tests close the deferred-integration gap flagged on PR #2165
    // (M12) + #2206 (M13). They use TestSessionHelper.CreateAdminSessionAsync
    // to fabricate a valid admin session token, then send requests via
    // CreateAuthenticatedRequest (mirror of AdminDomainEventOutboxEndpoints
    // test pattern from #1535 T6).
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListDeadLetters_Admin_EmptyTable_Returns200WithZeroItems()
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{EndpointBase}/dead-letters?skip=0&take=10",
            _adminSessionToken);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<WikidataDeadLetterAttemptsResult>(body, JsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Skip.Should().Be(0);
        result.Take.Should().Be(10);
    }

    [Fact]
    public async Task ListDeadLetters_Admin_PopulatedTable_ReturnsRows()
    {
        var gameId = await SeedDeadLetterAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{EndpointBase}/dead-letters?skip=0&take=10",
            _adminSessionToken);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<WikidataDeadLetterAttemptsResult>(body, JsonOptions);

        result!.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].SharedGameId.Should().Be(gameId);
        result.Items[0].Reason.Should().Be("r2-upload-error");
        result.Items[0].GameTitle.Should().Be("M16 Test Game");
    }

    [Fact]
    public async Task ListDeadLetters_Admin_ReasonFilter_Applied()
    {
        await SeedDeadLetterAsync("M16 r2-upload Game", reason: "r2-upload-error");
        await SeedDeadLetterAsync("M16 image-processing Game", reason: "image-processing-error");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{EndpointBase}/dead-letters?reason=image-processing-error",
            _adminSessionToken);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<WikidataDeadLetterAttemptsResult>(body, JsonOptions);

        result!.TotalCount.Should().Be(1);
        result.Items[0].Reason.Should().Be("image-processing-error");
    }

    private async Task<Guid> SeedDeadLetterAsync(
        string gameTitle = "M16 Test Game",
        string reason = "r2-upload-error")
    {
        var (gameId, _) = await SeedDeadLetterAttemptAsync(gameTitle, reason);
        return gameId;
    }

    /// <summary>
    /// Phase F sibling of <see cref="SeedDeadLetterAsync"/> that also surfaces
    /// the attempt id (required by F5 bulk-acknowledge tests, which pass attempt
    /// ids — NOT game ids — in the request body).
    /// </summary>
    private async Task<(Guid GameId, Guid AttemptId)> SeedDeadLetterAttemptAsync(
        string gameTitle = "M16 Test Game",
        string reason = "r2-upload-error")
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = gameTitle,
            YearPublished = 2020,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = TestAdminId,
            CreatedAt = DateTime.UtcNow,
            WikidataQid = "Q1",
        };
        dbContext.SharedGames.Add(game);

        var attempt = new WikidataCoverEnrichmentAttemptEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = game.Id,
            AttemptedAt = DateTime.UtcNow.AddHours(-1),
            Outcome = 3, // DeadLetter
            Reason = reason,
            Details = "503 service unavailable",
            RetryCount = 3,
            DeadLetteredAt = DateTime.UtcNow.AddHours(-1),
        };
        dbContext.WikidataCoverEnrichmentAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (game.Id, attempt.Id);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Phase F F5 (#2254) — bulk-acknowledge endpoint integration coverage.
    //
    // These tests close the F5 endpoint contract: anonymous probes get 401,
    // admin sessions ack a real dead-letter row (the result envelope reports
    // ackedCount=1 and the default list view immediately drops it), and
    // over-cap batches are rejected by the FluentValidation pipeline as 422
    // (the project-wide convention from Issue #1449 — the plan-text "400" is
    // shorthand for "validation rejection"; the ApiExceptionHandlerMiddleware
    // returns 422 for FluentValidation.ValidationException).
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkAcknowledge_AnonymousRequest_Returns401()
    {
        // Auth filter must reject anonymous probes BEFORE the MediatR pipeline.
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{EndpointBase}/bulk-acknowledge")
        {
            Content = JsonContent.Create(new
            {
                attemptIds = new[] { Guid.NewGuid() },
                note = (string?)null,
            }),
        };

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the bulk-acknowledge admin endpoint must require an admin session");
    }

    [Fact]
    public async Task BulkAcknowledge_AdminWithDeadLetter_AcksRowAndDropsFromDefaultList()
    {
        var (gameId, attemptId) = await SeedDeadLetterAttemptAsync(
            gameTitle: "F5 Acknowledge Game",
            reason: "r2-upload-error");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{EndpointBase}/bulk-acknowledge",
            _adminSessionToken,
            new
            {
                attemptIds = new[] { attemptId },
                note = "F5 endpoint smoke test",
            });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<AdminBulkAcknowledgeResult>(body, JsonOptions);

        result.Should().NotBeNull();
        result!.AckedCount.Should().Be(1);
        result.IdempotentNoOpCount.Should().Be(0);
        result.NotFoundCount.Should().Be(0);
        result.WrongStateCount.Should().Be(0);
        result.Rows.Should().ContainSingle();
        result.Rows[0].AttemptId.Should().Be(attemptId);
        result.Rows[0].GameId.Should().Be(gameId);
        result.Rows[0].Outcome.Should().Be(AdminBulkAcknowledgeRow.OutcomeAcked);

        // Direct DB verification: bypass the list endpoint to isolate the layer
        // (handler vs query). If THIS fails, the handler/repo persistence path
        // is broken; if the LIST endpoint check below fails while this passes,
        // the bug is in the query / filter.
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var persisted = await dbContext.WikidataCoverEnrichmentAttempts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attemptId, TestContext.Current.CancellationToken);
            persisted.Should().NotBeNull();
            persisted!.AcknowledgedAt.Should().NotBeNull(
                "the F5 handler+repo must persist AcknowledgedAt via SaveChangesAsync");
            persisted.AcknowledgedBy.Should().Be(TestAdminId,
                "the F5 handler must record the admin user id who acked the row");
        }

        // Default list view (includeAcknowledged omitted) hides the acked row.
        var listRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{EndpointBase}/dead-letters",
            _adminSessionToken);
        var listResponse = await _client.SendAsync(listRequest, TestContext.Current.CancellationToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var list = JsonSerializer.Deserialize<WikidataDeadLetterAttemptsResult>(listBody, JsonOptions);
        list!.Items.Should().NotContain(i => i.Id == attemptId,
            "default list view hides acknowledged rows so operators only see open work");

        // includeAcknowledged=true surfaces the row again for the audit view.
        var includedRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"{EndpointBase}/dead-letters?includeAcknowledged=true",
            _adminSessionToken);
        var includedResponse = await _client.SendAsync(includedRequest, TestContext.Current.CancellationToken);
        includedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var includedBody = await includedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var included = JsonSerializer.Deserialize<WikidataDeadLetterAttemptsResult>(includedBody, JsonOptions);
        var includedItem = included!.Items.Should().ContainSingle(i => i.Id == attemptId).Subject;
        includedItem.AcknowledgedAt.Should().NotBeNull("F5 ack metadata must round-trip via the audit view");
        includedItem.AcknowledgedBy.Should().Be(TestAdminId);
    }

    [Fact]
    public async Task BulkAcknowledge_OverFifty_ReturnsValidationError()
    {
        // FluentValidation rejects batches > MaxBatchSize. Issue #1449 convention:
        // FluentValidation.ValidationException → HTTP 422 (NOT 400) via
        // ApiExceptionHandlerMiddleware. Plan-text uses "400" as shorthand.
        var ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{EndpointBase}/bulk-acknowledge",
            _adminSessionToken,
            new
            {
                attemptIds = ids,
                note = (string?)null,
            });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "FluentValidation rejects > MaxBatchSize batches and the middleware maps to 422");
    }
}
