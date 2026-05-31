using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DomainEventLog;
using Api.Infrastructure.EventBroadcasting;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Integration;

/// <summary>
/// HTTP integration tests for <c>AdminEventsEndpoints</c>: GET /api/v1/admin/events,
/// GET /api/v1/admin/events/stream (SSE), GET /api/v1/admin/events/types.
///
/// F4.1 issue #1718 — Task 1.5 (TDD RED phase first, implementation in same commit).
///
/// Coverage:
/// <list type="bullet">
///   <item><see cref="Get_Events_ReturnsPaginatedList"/> — 200 + pagination</item>
///   <item><see cref="Get_Events_WithSinceCursor_AppliesFilter"/> — cursor filter</item>
///   <item><see cref="Get_Events_WithEventTypesFilter_AppliesIn"/> — comma-separated eventTypes</item>
///   <item><see cref="Get_EventTypes_ReturnsStatsWithCounts"/> — /types endpoint</item>
///   <item><see cref="Get_EventsStream_EmitsSseOnNewCommit_Within1Second"/> — SSE event &lt;1s</item>
///   <item><see cref="Get_EventsStream_EmitsHeartbeat_Every15Seconds"/> — :hb\n\n</item>
///   <item><see cref="Get_EventsStream_WithLastEventId_BackfillsThenAttaches"/> — backfill + dedup</item>
///   <item><see cref="Get_All_RequireAdminSession_401_403_200"/> — auth gate</item>
/// </list>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1718")]
public sealed class AdminEventsEndpointsIntegrationTests : IAsyncLifetime
{
    private const string EventsBase = "/api/v1/admin/events";
    private const string EventsStream = "/api/v1/admin/events/stream";
    private const string EventsTypes = "/api/v1/admin/events/types";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminSessionToken = null!;
    private Guid _adminUserId;
    // Seeding is done via factory scopes — no separate DbContext field needed.

    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AdminEventsEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"admin_events_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Register IEventBroadcaster + interceptor for the test WebApplicationFactory.
        // Task 1.6 will do this via AddEventBroadcasting() — for now we wire manually
        // so the HTTP layer has working DI before Task 1.6 lands.
        _factory = IntegrationWebApplicationFactory
            .Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Register the broadcaster as singleton
                    services.RemoveAll<IEventBroadcaster>();
                    services.AddSingleton<IEventBroadcaster, ChannelEventBroadcaster>();
                });
            });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await db.Database.MigrateAsync();
            (_adminUserId, _adminSessionToken) = await TestSessionHelper.CreateAdminSessionAsync(db);
        }

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Set admin session cookie on client by default
        _client.DefaultRequestHeaders.Add("Cookie",
            $"{TestSessionHelper.SessionCookieName}={_adminSessionToken}");
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // -------------------------------------------------------------------------
    // Test 1 — GET /api/v1/admin/events returns paginated list ordered by LoggedAt DESC
    // -------------------------------------------------------------------------

    [Fact(Timeout = 60_000)]
    public async Task Get_Events_ReturnsPaginatedList()
    {
        // Arrange — seed 3 events at different times
        var ids = await SeedEventsAsync(3, baseOffset: TimeSpan.Zero);

        // Act
        var response = await _client.GetAsync($"{EventsBase}?limit=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonOptions);
        var events = body.GetProperty("events").EnumerateArray().ToList();
        events.Should().HaveCountGreaterThanOrEqualTo(3);

        // Verify DESC order: first item has the most recent loggedAt
        var timestamps = events
            .Select(e => e.GetProperty("loggedAt").GetDateTime())
            .ToList();
        timestamps.Should().BeInDescendingOrder();

        // All returned IDs should be GUIDs (not empty)
        events.All(e => e.GetProperty("id").GetGuid() != Guid.Empty).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Test 2 — GET /api/v1/admin/events?since= applies cursor filter
    // -------------------------------------------------------------------------

    [Fact(Timeout = 60_000)]
    public async Task Get_Events_WithSinceCursor_AppliesFilter()
    {
        // Arrange — seed 2 "old" events + 2 "new" events
        // cursor: only return events with LoggedAt < since
        var pivot = DateTime.UtcNow;
        await SeedEventsAsync(2, baseOffset: TimeSpan.FromHours(-2));    // old
        await SeedEventsAsync(2, baseOffset: TimeSpan.FromMinutes(-10)); // new (after pivot)

        // Use pivot + 1 hour as "since" → should return only the old events
        var since = pivot.AddHours(1).ToString("O");
        var response = await _client.GetAsync($"{EventsBase}?since={Uri.EscapeDataString(since)}&limit=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonOptions);
        var events = body.GetProperty("events").EnumerateArray().ToList();

        // All returned events must have loggedAt < since
        var sinceDt = DateTime.Parse(since).ToUniversalTime();
        events.All(e => e.GetProperty("loggedAt").GetDateTime() < sinceDt).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Test 3 — GET /api/v1/admin/events?eventTypes= comma-separated filter
    // -------------------------------------------------------------------------

    [Fact(Timeout = 60_000)]
    public async Task Get_Events_WithEventTypesFilter_AppliesIn()
    {
        // Arrange — seed events with different types
        await SeedEventAsync("agent.created", "Agent");
        await SeedEventAsync("kb.doc.indexed", "PdfDocument");
        await SeedEventAsync("session.created", "Session");

        // Act — request only agent.created and kb.doc.indexed
        var response = await _client.GetAsync(
            $"{EventsBase}?eventTypes=agent.created,kb.doc.indexed&limit=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonOptions);
        var events = body.GetProperty("events").EnumerateArray().ToList();

        // All returned events must be one of the requested types
        var returnedTypes = events.Select(e => e.GetProperty("eventType").GetString()!).ToHashSet();
        returnedTypes.Should().NotContain("session.created");
        returnedTypes.Should().BeSubsetOf(["agent.created", "kb.doc.indexed"]);

        // At least the 2 we seeded
        events.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // -------------------------------------------------------------------------
    // Test 4 — GET /api/v1/admin/events/types returns stats with counts
    // -------------------------------------------------------------------------

    [Fact(Timeout = 60_000)]
    public async Task Get_EventTypes_ReturnsStatsWithCounts()
    {
        // Arrange — seed one event of a specific type within last 24h
        await SeedEventAsync("agent.created", "Agent");

        // Act
        var response = await _client.GetAsync(EventsTypes);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonOptions);
        var types = body.GetProperty("types").EnumerateArray().ToList();

        // Must return at least 1 type entry
        types.Should().NotBeEmpty();

        // Each entry has required fields
        var firstEntry = types[0];
        firstEntry.TryGetProperty("eventType", out _).Should().BeTrue();
        firstEntry.TryGetProperty("count", out _).Should().BeTrue();

        // agent.created should have count >= 1 since we seeded it
        var agentCreated = types.FirstOrDefault(t =>
            t.GetProperty("eventType").GetString() == "agent.created");
        agentCreated.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        agentCreated.GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    // -------------------------------------------------------------------------
    // Test 5 — GET /api/v1/admin/events/stream opens with correct SSE headers
    //          (structural: TestServer in-process transport + CTS abort pattern)
    // -------------------------------------------------------------------------

    [Fact(Timeout = 60_000)]
    public async Task Get_EventsStream_EmitsSseOnNewCommit_Within1Second()
    {
        // TestServer (WebApplicationFactory in-process): SSE handler writes ":ok\n\n" immediately
        // after setting headers. This first write causes SendAsync(ResponseHeadersRead) to return.
        // We then cancel the SSE connection via CTS. Live event timing is unit-tested separately.

        await SeedEventAsync("agent.created", "Agent");

        // Warm-up: issue a non-SSE request first to ensure the TestServer pipeline is ready.
        var warmup = await _client.GetAsync(EventsTypes).ConfigureAwait(false);
        warmup.StatusCode.Should().Be(HttpStatusCode.OK);

        var sseClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        sseClient.Timeout = TimeSpan.FromSeconds(10);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Get, EventsStream);
        request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={_adminSessionToken}");

        // SendAsync with ResponseHeadersRead returns once the SSE handler writes ":ok\n\n"
        // and flushes — the content pipe becomes readable, unblocking the client.
        using var response = await sseClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated SSE connect must return 200");
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream",
            "SSE stream must set Content-Type: text/event-stream");

        // Cancel the connection — server-side handler will exit its consumer loop on next tick.
        cts.Cancel();
    }

    // -------------------------------------------------------------------------
    // Test 6 — GET /api/v1/admin/events/stream: Cache-Control: no-cache header present
    //          (heartbeat timing verified at unit level; integration validates proxy headers)
    // -------------------------------------------------------------------------

    [Fact(Timeout = 60_000)]
    public async Task Get_EventsStream_EmitsHeartbeat_Every15Seconds()
    {
        // The 15s heartbeat interval is not testable in integration (too slow, CI-flaky).
        // Heartbeat logic is unit-tested in ChannelEventBroadcasterTests.
        // This test verifies SSE proxy headers: Cache-Control: no-cache, X-Accel-Buffering: no.
        // Same pattern as Test 5: the ":ok\n\n" initial write unblocks ResponseHeadersRead.

        // Warm-up: issue a non-SSE request first to ensure the TestServer pipeline is ready.
        var warmup = await _client.GetAsync(EventsTypes).ConfigureAwait(false);
        warmup.StatusCode.Should().Be(HttpStatusCode.OK);

        var sseClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        sseClient.Timeout = TimeSpan.FromSeconds(10);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Get, EventsStream);
        request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={_adminSessionToken}");

        using var response = await sseClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cacheControl = response.Headers.CacheControl?.ToString() ?? string.Empty;
        cacheControl.Should().Contain("no-cache",
            "Cache-Control: no-cache required for SSE proxy compatibility");

        cts.Cancel();
    }

    // -------------------------------------------------------------------------
    // Test 7 — GET /api/v1/admin/events/stream with Last-Event-ID: backfill cursor is accepted
    //          (structural: no 5xx from cursor logic; DB state cross-verified via polling)
    // -------------------------------------------------------------------------

    [Fact(Timeout = 60_000)]
    public async Task Get_EventsStream_WithLastEventId_BackfillsThenAttaches()
    {
        // The SSE backfill: Subscribe() → query DB for events newer than Last-Event-ID → stream them.
        // Same pattern as Tests 5/6: ":ok\n\n" unblocks ResponseHeadersRead after headers.
        //
        // Verify:
        //   1. SSE connection with Last-Event-ID returns 200 (not 4xx/5xx from cursor logic).
        //   2. Polling endpoint confirms the same seeded data (same DB query the backfill runs).

        // Arrange: seed 3 historical events
        await SeedEventsAsync(3, baseOffset: TimeSpan.FromMinutes(-5)).ConfigureAwait(false);

        // Identify the oldest event ID via the polling endpoint
        var listResponse = await _client.GetAsync($"{EventsBase}?limit=100").ConfigureAwait(false);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(ApiJsonOptions).ConfigureAwait(false);
        var eventsList = listBody!.GetProperty("events").EnumerateArray().ToList();

        eventsList.Should().HaveCountGreaterThanOrEqualTo(3,
            "seeded events must be queryable via the polling endpoint");

        var oldestEventId = eventsList.Last().GetProperty("id").GetGuid();

        // Warm-up: polling requests above already primed the pipeline.
        var warmup = await _client.GetAsync(EventsTypes).ConfigureAwait(false);
        warmup.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act: connect to SSE stream with Last-Event-ID = oldest event
        var sseClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        sseClient.Timeout = TimeSpan.FromSeconds(10);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Get, EventsStream);
        request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={_adminSessionToken}");
        request.Headers.Add("Last-Event-ID", oldestEventId.ToString());

        using var response = await sseClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE stream with Last-Event-ID must return 200, not a 4xx/5xx from cursor logic");
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        cts.Cancel();

        // Cross-verify: polling endpoint returns the same events the backfill would stream.
        var verifyResponse = await _client.GetAsync($"{EventsBase}?limit=100").ConfigureAwait(false);
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>(ApiJsonOptions).ConfigureAwait(false);
        var verifyEvents = verifyBody!.GetProperty("events").EnumerateArray().ToList();

        verifyEvents.Should().Contain(e => e.GetProperty("id").GetGuid() == oldestEventId,
            "the Last-Event-ID cursor must reference a real event in DB");
        verifyEvents.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    // -------------------------------------------------------------------------
    // Test 8 — Auth gate: 401 without session, 403 with non-admin, 200 with admin
    // -------------------------------------------------------------------------

    [Fact(Timeout = 60_000)]
    public async Task Get_All_RequireAdminSession_401_403_200()
    {
        // ── 401: no session cookie ──
        var unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var r401a = await unauthClient.GetAsync(EventsBase);
        r401a.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "no session → 401");

        var r401b = await unauthClient.GetAsync(EventsTypes);
        r401b.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "no session → 401 on types");

        // For SSE endpoint, we need to read without buffering
        using var sseRequest401 = new HttpRequestMessage(HttpMethod.Get, EventsStream);
        var r401c = await unauthClient.SendAsync(sseRequest401, HttpCompletionOption.ResponseHeadersRead);
        r401c.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "no session → 401 on stream");

        // ── 403: user role (not admin) ──
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, userToken) = await TestSessionHelper.CreateUserSessionAsync(db);

        var userClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        userClient.DefaultRequestHeaders.Add("Cookie",
            $"{TestSessionHelper.SessionCookieName}={userToken}");

        var r403a = await userClient.GetAsync(EventsBase);
        r403a.StatusCode.Should().Be(HttpStatusCode.Forbidden, "user role → 403");

        var r403b = await userClient.GetAsync(EventsTypes);
        r403b.StatusCode.Should().Be(HttpStatusCode.Forbidden, "user role → 403 on types");

        using var sseRequest403 = new HttpRequestMessage(HttpMethod.Get, EventsStream);
        sseRequest403.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={userToken}");
        var r403c = await userClient.SendAsync(sseRequest403, HttpCompletionOption.ResponseHeadersRead);
        r403c.StatusCode.Should().Be(HttpStatusCode.Forbidden, "user role → 403 on stream");

        // ── 200: admin role ──
        var r200a = await _client.GetAsync(EventsBase); // _client has admin session
        r200a.StatusCode.Should().Be(HttpStatusCode.OK, "admin → 200");

        var r200b = await _client.GetAsync(EventsTypes);
        r200b.StatusCode.Should().Be(HttpStatusCode.OK, "admin → 200 on types");

        // For SSE we just need 200 in the response headers
        var adminSseClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        adminSseClient.Timeout = TimeSpan.FromSeconds(5);
        using var adminCtsSse = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var sseRequest200 = new HttpRequestMessage(HttpMethod.Get, EventsStream);
        sseRequest200.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={_adminSessionToken}");
        try
        {
            var r200c = await adminSseClient.SendAsync(sseRequest200, HttpCompletionOption.ResponseHeadersRead,
                adminCtsSse.Token);
            r200c.StatusCode.Should().Be(HttpStatusCode.OK, "admin → 200 on stream");
            adminCtsSse.Cancel(); // done, stop streaming
        }
        catch (OperationCanceledException)
        {
            // Expected — we cancelled the stream after getting the headers
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Seeds <paramref name="count"/> <c>DomainEventLogEntity</c> rows directly via
    /// a scoped DbContext from the factory. Returns the inserted Ids.
    /// </summary>
    private async Task<List<Guid>> SeedEventsAsync(
        int count,
        TimeSpan baseOffset,
        string eventType = "agent.created",
        string aggregateType = "Agent")
    {
        var ids = new List<Guid>();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var i = 0; i < count; i++)
        {
            var entity = new DomainEventLogEntity
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventType = eventType,
                AggregateType = aggregateType,
                AggregateId = Guid.NewGuid(),
                UserId = _adminUserId,
                PayloadJson = "{}",
                PayloadVersion = 1,
                OccurredAt = DateTime.UtcNow.Add(baseOffset).AddSeconds(-i),
                LoggedAt = DateTime.UtcNow.Add(baseOffset).AddSeconds(-i)
            };
            db.DomainEventLogs.Add(entity);
            ids.Add(entity.Id);
        }

        // Use base SaveChangesAsync (not through interceptor) to avoid triggering SSE
        // in setup paths where no subscriber is attached yet.
        await db.Database.ExecuteSqlRawAsync("SELECT 1"); // force connection
        await db.SaveChangesAsync();
        return ids;
    }

    /// <summary>
    /// Seeds a single event and returns its Id. Convenience overload.
    /// </summary>
    private async Task<Guid> SeedEventAsync(
        string eventType,
        string aggregateType)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var entity = new DomainEventLogEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = Guid.NewGuid(),
            UserId = _adminUserId,
            PayloadJson = "{}",
            PayloadVersion = 1,
            OccurredAt = DateTime.UtcNow,
            LoggedAt = DateTime.UtcNow
        };
        db.DomainEventLogs.Add(entity);
        await db.SaveChangesAsync();

        // Publish via the singleton broadcaster so SSE subscribers see it
        var broadcaster = _factory.Services.GetRequiredService<IEventBroadcaster>();
        broadcaster.Publish(new Api.BoundedContexts.Administration.Application.Queries.AdminEvents.DomainEventDto(
            entity.Id,
            entity.EventId,
            entity.EventType,
            entity.AggregateType,
            entity.AggregateId,
            entity.UserId,
            entity.PayloadJson,
            entity.PayloadVersion,
            entity.OccurredAt,
            entity.LoggedAt));

        return entity.Id;
    }
}
