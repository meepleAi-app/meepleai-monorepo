using System.Net;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
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

namespace Api.Tests.Integration.SessionTracking;

/// <summary>
/// Issue #2561 SP2 T11: Verify that GET /api/v1/game-sessions/{id}/stream/v2
/// carries expand-and-contract deprecation headers (<c>Deprecation</c>, <c>Sunset</c>,
/// <c>Link rel="successor-version"</c>) before the SSE body starts.
///
/// The endpoint is still fully functional (expand-and-contract — NOT removal).
/// Headers must appear regardless of whether the SSE stream subsequently carries events.
///
/// Uses <c>HttpCompletionOption.ResponseHeadersRead</c> so the client reads
/// response headers without waiting for the SSE stream to end (which it never does).
/// CancellationToken is used to abort the request after headers are inspected.
///
/// Requires Testcontainers (Docker) — CI gate only.
///
/// Session-seeding strategy mirrors <see cref="LiveSessionStreamEndpointTests"/>:
/// seed a real <see cref="SessionEntity"/> row that the authenticated user owns,
/// so <c>GetSessionStreamQuery</c> finds it and the handler reaches the header-writing
/// block at SessionQueryEndpoints.cs:364-368.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "2561")]
public sealed class StreamV2DeprecationHeadersEndpointTests : IAsyncLifetime
{
    private const string StreamV2Path = "/api/v1/game-sessions/{0}/stream/v2";

    // Sunset date chosen in MapEnhancedSessionStreamEndpoint — keep in sync.
    // ~3 months post SP2 merge (2026-06-29). Exact date: Mon, 29 Sep 2026 00:00:00 GMT.
    private const string ExpectedSunset = "Mon, 29 Sep 2026 00:00:00 GMT";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public StreamV2DeprecationHeadersEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"stream_v2_deprecation_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory
            .Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Mock ISessionBroadcastService so:
                    //   - GetConnectionCount returns 0 (below the 20-connection pool limit)
                    //   - SubscribeAsync returns an empty SSE stream (stream exits immediately)
                    services.RemoveAll<ISessionBroadcastService>();
                    var mockBroadcast = new Mock<ISessionBroadcastService>();
                    mockBroadcast
                        .Setup(s => s.GetConnectionCount(It.IsAny<Guid>()))
                        .Returns(0);
                    mockBroadcast
                        .Setup(s => s.SubscribeAsync(
                            It.IsAny<Guid>(),
                            It.IsAny<Guid>(),
                            It.IsAny<string?>(),
                            It.IsAny<CancellationToken>()))
                        .Returns(EmptyAsyncEnumerable());
                    services.AddSingleton(mockBroadcast.Object);

                    // Mock ISessionSyncService so GetSessionStreamQueryHandler.Handle
                    // succeeds without a real Redis/in-memory channel.
                    // The endpoint ignores the IAsyncEnumerable<INotification> return value —
                    // it calls ISessionBroadcastService.SubscribeAsync directly — but the
                    // handler still needs ISessionSyncService injected to compile.
                    services.RemoveAll<ISessionSyncService>();
                    var mockSync = new Mock<ISessionSyncService>();
                    mockSync
                        .Setup(s => s.SubscribeToSessionEvents(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                        .Returns(EmptyNotificationEnumerable());
                    services.AddSingleton(mockSync.Object);
                });
            });

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // Do NOT follow redirects — let us see 401 directly.
            AllowAutoRedirect = false,
        });
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    // ── Test 1: unauthenticated request MUST NOT carry deprecation headers ──────────────────

    [Fact]
    public async Task StreamV2_Unauthenticated_ReturnsUnauthorized_WithoutDeprecationHeaders()
    {
        var sessionId = Guid.NewGuid();
        var url = string.Format(StreamV2Path, sessionId);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // The endpoint MUST return 401 for unauthenticated callers — auth guard fires before headers.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "unauthenticated callers must be rejected before deprecation headers are written");

        // Deprecation headers MUST NOT appear on a 401 response.
        response.Headers.Contains("Deprecation").Should().BeFalse(
            "deprecation headers are only set after successful auth+authz, not on 401");
        response.Headers.Contains("Sunset").Should().BeFalse();
        response.Headers.Contains("Link").Should().BeFalse();
    }

    // ── Test 2: authenticated owner request MUST reach 200 and carry deprecation headers ────
    //
    // Seeding strategy (mirrors LiveSessionStreamEndpointTests):
    //   1. Create a user + auth session via TestSessionHelper.CreateUserSessionAsync.
    //   2. Insert a SessionEntity owned by that user directly into the DB.
    //   3. Make a GET request as that user to /stream/v2/{ownedSessionId}.
    //   4. GetSessionStreamQuery finds the session (isOwner = true) → no NotFoundException.
    //   5. The handler reaches the deprecation-header block (SessionQueryEndpoints.cs:364-368).
    //   6. Assert Deprecation, Sunset, and Link headers on the 200 response.
    //
    // This test MUST fail if the deprecation headers are removed from the endpoint.

    [Fact]
    public async Task StreamV2_AuthenticatedOwner_ReceivesDeprecationHeaders_On200()
    {
        // ── Arrange: seed user + owned SessionTracking session ──────────────────
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Create a user and a valid auth session (returns userId + raw cookie token).
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Seed a SharedGame to satisfy the SessionEntity.GameId FK. Although the C#
        // property SessionEntity.GameId is Guid?, the column game_id is NOT NULL and
        // carries a foreign key to shared_games with DeleteBehavior.Restrict
        // (SessionConfiguration.cs:27-29,155-159). A random Guid would fail the FK,
        // so we seed a real shared game and reference its id. (Issue #2596)
        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, "StreamV2 Deprecation Test Game");

        // Seed a SessionEntity owned by that user so GetSessionStreamQuery succeeds.
        var trackingSessionId = Guid.NewGuid();
        dbContext.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = trackingSessionId,
            UserId = userId,
            GameId = gameId,
            SessionCode = "TST001",
            // Must be a valid SessionType enum member ("Generic" | "GameSpecific") —
            // SessionMapper.ToDomain does Enum.Parse<SessionType>, so an invalid value
            // (the previous "Standard") throws ArgumentException → 400. With a GameId set,
            // "GameSpecific" is the semantically correct value. (Issue #2596)
            SessionType = "GameSpecific",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            ScoringType = "Points",
            ScoreData = "{}",
        });
        await dbContext.SaveChangesAsync();

        var url = string.Format(StreamV2Path, trackingSessionId);

        // ── Act: authenticated request using ResponseHeadersRead ─────────────────
        // We read only the response headers — the SSE body is infinite and we
        // cancel immediately after reading headers (mirrors LiveSessionStreamEndpointTests).
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, url, sessionToken);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // ── Assert ───────────────────────────────────────────────────────────────
        // The session exists and the user is its owner → GetSessionStreamQuery succeeds
        // → the handler writes deprecation headers → 200 text/event-stream.
        // If this fails with 401, the auth middleware did not honor the seeded cookie
        // (a test-infrastructure failure, not a production regression).
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"authenticated owner of session {trackingSessionId} must reach the streaming path " +
            $"(401 = cookie not honored by test host; 404 = session row missing — both indicate " +
            $"test-infrastructure issues, not a production regression)");

        // All three deprecation headers MUST be present on the 200 response.
        AssertDeprecationHeaders(response, trackingSessionId);
    }

    // ── Test 3: headers absent before 200 (verify ordering) ─────────────────────────────────

    [Fact]
    public async Task StreamV2_Unauthenticated_NoDeprecationHeaders_SentBeforeAuth()
    {
        // Sanity-check counterpart of Test 2: an unauthenticated request to a
        // non-existent session MUST NOT carry deprecation headers (the headers are
        // only appended after auth+session-lookup succeeds).
        var sessionId = Guid.NewGuid();
        var url = string.Format(StreamV2Path, sessionId);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Contains("Deprecation").Should().BeFalse();
        response.Headers.Contains("Sunset").Should().BeFalse();
        response.Headers.Contains("Link").Should().BeFalse();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static void AssertDeprecationHeaders(HttpResponseMessage response, Guid sessionId)
    {
        // Deprecation: true
        response.Headers.Should().ContainKey("Deprecation",
            "expand-and-contract requires Deprecation header on /stream/v2 (Issue #2561 SP2 T11)");
        response.Headers.GetValues("Deprecation").Should().ContainSingle()
            .Which.Should().Be("true");

        // Sunset: Mon, 29 Sep 2026 00:00:00 GMT
        response.Headers.Should().ContainKey("Sunset",
            "expand-and-contract requires Sunset header indicating planned removal date");
        response.Headers.GetValues("Sunset").Should().ContainSingle()
            .Which.Should().Be(ExpectedSunset);

        // Link: </api/v1/live-sessions/{id}/stream>; rel="successor-version"
        response.Headers.Should().ContainKey("Link",
            "expand-and-contract requires Link header pointing to the successor route");
        var linkHeader = response.Headers.GetValues("Link").Should().ContainSingle().Subject;
        linkHeader.Should().Contain($"/api/v1/live-sessions/{sessionId}/stream",
            "Link must reference the canonical native stream route for this specific session");
        linkHeader.Should().Contain("successor-version",
            "Link rel must be 'successor-version' per RFC 8594");
    }

    private static async IAsyncEnumerable<SseEventEnvelope> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<INotification> EmptyNotificationEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }
}
