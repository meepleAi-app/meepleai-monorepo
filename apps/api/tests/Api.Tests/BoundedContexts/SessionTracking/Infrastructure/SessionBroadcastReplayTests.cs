using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure;

/// <summary>
/// T7: Durable Redis-backed replay buffer tests.
/// Issue #2561 SP2 T7.
///
/// Test taxonomy:
/// - [Trait("Category", "Unit")]     — no Docker required; exercise in-memory fallback path.
/// - [Trait("Category", "Integration")] — require Docker+Redis (Testcontainers.Redis).
///   CI gate: dotnet test --filter "Category=Integration&amp;BoundedContext=SessionTracking".
///   Local: requires Docker daemon. Skip-reason captured in test output when Docker unavailable.
/// </summary>
public sealed class SessionBroadcastReplayTests
{
    // =========================================================================
    // GUARD TESTS — in-memory fallback (no Redis, no Docker required)
    // These MUST pass on every run, including CI shards with no Docker.
    // =========================================================================

    #region Guard: in-memory fallback still passes (backward-compat)

    /// <summary>
    /// Guard test: the existing SubscribeAsync_WithLastEventId_ReplaysBufferedEvents scenario
    /// must continue to work via the in-memory CircularEventBuffer fallback when Redis is absent.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "SessionTracking")]
    public async Task InMemory_Reconnect_replays_only_events_after_lastEventId()
    {
        // Arrange: no Redis — in-memory fallback path
        var svc = CreateInMemoryService();
        var sid = Guid.NewGuid();
        var uid = Guid.NewGuid();

        // Keep-alive subscriber to prevent pool cleanup between disconnect and reconnect
        using var keepAliveCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var keepAliveTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in svc.SubscribeAsync(sid, Guid.NewGuid(), null, keepAliveCts.Token)) { }
            }
            catch (OperationCanceledException) { }
        }, keepAliveCts.Token);

        await Task.Delay(50);

        // Collect 3 events
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ids = new List<string>();
        var sub1 = Task.Run(async () =>
        {
            await foreach (var evt in svc.SubscribeAsync(sid, uid, null, cts1.Token))
            {
                ids.Add(evt.Id);
                if (ids.Count >= 3) break;
            }
        }, cts1.Token);

        await Task.Delay(50);

        for (var i = 0; i < 3; i++)
        {
            await svc.PublishAsync(sid, MakeScoreEvent(sid, uid), EventVisibility.Public);
            await Task.Delay(10);
        }

        await sub1;
        ids.Should().HaveCount(3);

        // Act: reconnect with lastEventId = ids[0] → should replay ids[1], ids[2]
        using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var replayed = new List<string>();
        try
        {
            await foreach (var evt in svc.SubscribeAsync(sid, uid, ids[0], cts2.Token))
            {
                replayed.Add(evt.Id);
                if (replayed.Count >= 2) break;
            }
        }
        catch (OperationCanceledException) { }

        await keepAliveCts.CancelAsync();
        try { await keepAliveTask; } catch (OperationCanceledException) { }

        // Assert
        replayed.Should().HaveCount(2);
        replayed[0].Should().Be(ids[1]);
        replayed[1].Should().Be(ids[2]);
    }

    /// <summary>
    /// Guard test: invalid (non-numeric) lastEventId with in-memory fallback must replay nothing,
    /// preserving the existing "id not found → empty" contract.
    /// With T7's TryParse guard: a garbage id fails TryParse → falls into in-memory
    /// CircularEventBuffer.GetSince which also returns [] for unknown ids.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "SessionTracking")]
    public async Task InMemory_InvalidLastEventId_replays_nothing()
    {
        var svc = CreateInMemoryService();
        var sid = Guid.NewGuid();
        var uid = Guid.NewGuid();

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<SseEventEnvelope>();
        var sub = Task.Run(async () =>
        {
            await foreach (var evt in svc.SubscribeAsync(sid, uid, null, cts1.Token))
            {
                received.Add(evt);
                if (received.Count >= 1) break;
            }
        }, cts1.Token);

        await Task.Delay(50);
        await svc.PublishAsync(sid, MakeScoreEvent(sid, uid), EventVisibility.Public);
        await sub;

        // Act: reconnect with garbage id
        using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var replayed = new List<SseEventEnvelope>();
        try
        {
            await foreach (var evt in svc.SubscribeAsync(sid, uid, "invalid-id", cts2.Token))
            {
                replayed.Add(evt);
            }
        }
        catch (OperationCanceledException) { }

        // Assert: nothing replayed — matches pre-T7 guard contract
        replayed.Should().BeEmpty();
    }

    #endregion

    // =========================================================================
    // REDIS INTEGRATION TESTS — require Docker (Testcontainers.Redis)
    // Marked [Trait("Category", "Integration")] so CI can gate separately.
    // =========================================================================

    #region Redis integration: cross-instance replay

    /// <summary>
    /// Publishes 3 events via service-A (Redis-backed), then service-B (same Redis, different
    /// in-process instance) reconnects with lastEventId = ids[0] and must receive ids[1]+ids[2].
    /// This is the core cross-instance replay scenario.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("BoundedContext", "SessionTracking")]
    public async Task Redis_CrossInstance_replay_returns_events_after_lastEventId()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        if (fixture is null)
        {
            // Observable skip (NOT a silent no-op pass) so a green run never masks an unexecuted test.
            Assert.Skip("Docker/Redis unavailable — Testcontainers fixture could not start.");
            return;
        }

        var sid = Guid.NewGuid();
        var uid = Guid.NewGuid();

        // Instance A: publishes 3 events
        using var svcA = CreateRedisService(fixture.Multiplexer);
        var ids = new List<string>();

        using var keepAliveCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var keepAlive = Task.Run(async () =>
        {
            try { await foreach (var _ in svcA.SubscribeAsync(sid, Guid.NewGuid(), null, keepAliveCts.Token)) { } }
            catch (OperationCanceledException) { }
        }, keepAliveCts.Token);
        await Task.Delay(50);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sub = Task.Run(async () =>
        {
            await foreach (var evt in svcA.SubscribeAsync(sid, uid, null, cts1.Token))
            {
                ids.Add(evt.Id);
                if (ids.Count >= 3) break;
            }
        }, cts1.Token);
        await Task.Delay(50);

        for (var i = 0; i < 3; i++)
        {
            await svcA.PublishAsync(sid, MakeScoreEvent(sid, uid), EventVisibility.Public);
            await Task.Delay(10);
        }
        await sub;
        ids.Should().HaveCount(3);

        await keepAliveCts.CancelAsync();
        try { await keepAlive; } catch (OperationCanceledException) { }

        // Instance B: new in-process service (different _instanceId, fresh _pools) — same Redis
        using var svcB = CreateRedisService(fixture.Multiplexer);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var replayed = new List<string>();
        try
        {
            await foreach (var evt in svcB.SubscribeAsync(sid, uid, ids[0], cts2.Token))
            {
                replayed.Add(evt.Id);
                if (replayed.Count >= 2) break;
            }
        }
        catch (OperationCanceledException) { }

        // Assert: cross-instance replay returned ids[1] and ids[2] in order
        replayed.Should().HaveCount(2);
        replayed[0].Should().Be(ids[1]);
        replayed[1].Should().Be(ids[2]);
    }

    /// <summary>
    /// After publishing >100 events via PublishEnvelopeAsync (bypasses in-pool rate limiter),
    /// the ZSET keeps only the 100 newest (cap eviction).
    /// We publish directly via PublishEnvelopeAsync without a live subscriber so the rate limiter
    /// in SessionSubscriptionPool is not involved (no pool exists) — the Lua cap logic is the
    /// behaviour under test.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("BoundedContext", "SessionTracking")]
    public async Task Redis_CapEviction_keeps_only_newest_100_events()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        if (fixture is null)
        {
            // Observable skip (NOT a silent no-op pass) so a green run never masks an unexecuted test.
            Assert.Skip("Docker/Redis unavailable — Testcontainers fixture could not start.");
            return;
        }

        var sid = Guid.NewGuid();
        using var svc = CreateRedisService(fixture.Multiplexer);

        // Publish 110 envelopes directly (no subscriber → rate limiter not invoked).
        // PublishEnvelopeAsync always writes to the Redis ZSET before PublishLocally.
        const int total = 110;
        var seqs = new List<long>();

        // We need the assigned ids, but since no subscriber exists the events go to the ZSET only.
        // To capture the ids, subscribe first with a throw-away receiver then publish after.
        // Simpler: use seq provider directly to know what seqs were allocated, then verify ZSET.
        // Even simpler: just publish and read back min/max seq from the ZSET itself.
        for (var i = 0; i < total; i++)
        {
            await svc.PublishAsync(sid, MakeScoreEvent(sid, Guid.NewGuid()), EventVisibility.Public);
        }

        // Verify ZSET cardinality directly via Redis
        var db = fixture.Multiplexer.GetDatabase();
        var key = Api.SharedKernel.Constants.RedisKeyConstants.GetSessionReplayKey(sid);
        var count = await db.SortedSetLengthAsync(key);

        // Cap = EventBufferSize = 100; ZSET must have been evicted to exactly 100
        count.Should().Be(SessionBroadcastService.EventBufferSize,
            because: $"Lua ZREMRANGEBYRANK must cap the ZSET to {SessionBroadcastService.EventBufferSize} entries after {total} publishes");

        // The 10 oldest entries (seqs 1-10) must have been evicted; min seq must be 11
        var allEntries = await db.SortedSetRangeByScoreWithScoresAsync(key);
        var minScore = (long)allEntries.Min(e => e.Score);
        var maxScore = (long)allEntries.Max(e => e.Score);

        // seqs are 1..110; after eviction keep 11..110
        minScore.Should().Be(total - SessionBroadcastService.EventBufferSize + 1,
            because: "oldest 10 events must have been evicted by Lua cap");
        maxScore.Should().Be(total, because: "newest event must be seq=110");
    }

    /// <summary>
    /// A legacy or garbage lastEventId that fails long.TryParse must trigger full buffer replay
    /// (falls through to in-memory CircularEventBuffer), not an error, not an exception.
    /// When connecting to a Redis-backed service, the pool may be fresh (empty buffer) → empty result.
    /// The important thing: no exception and the service remains functional.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("BoundedContext", "SessionTracking")]
    public async Task Redis_GarbageLastEventId_triggers_graceful_fallback()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        if (fixture is null)
        {
            // Observable skip (NOT a silent no-op pass) so a green run never masks an unexecuted test.
            Assert.Skip("Docker/Redis unavailable — Testcontainers fixture could not start.");
            return;
        }

        var sid = Guid.NewGuid();
        var uid = Guid.NewGuid();
        using var svc = CreateRedisService(fixture.Multiplexer);

        // Publish one event so there is something in the buffer
        using var keepAliveCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var keepAlive = Task.Run(async () =>
        {
            try { await foreach (var _ in svc.SubscribeAsync(sid, Guid.NewGuid(), null, keepAliveCts.Token)) { } }
            catch (OperationCanceledException) { }
        }, keepAliveCts.Token);
        await Task.Delay(50);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<SseEventEnvelope>();
        var sub = Task.Run(async () =>
        {
            await foreach (var evt in svc.SubscribeAsync(sid, uid, null, cts1.Token))
            {
                received.Add(evt);
                if (received.Count >= 1) break;
            }
        }, cts1.Token);
        await Task.Delay(50);

        await svc.PublishAsync(sid, MakeScoreEvent(sid, uid), EventVisibility.Public);
        await sub;

        await keepAliveCts.CancelAsync();
        try { await keepAlive; } catch (OperationCanceledException) { }

        // Act: reconnect with garbage lastEventId (fails TryParse) — must not throw
        using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var replayed = new List<SseEventEnvelope>();
        var threwException = false;

        try
        {
            await foreach (var evt in svc.SubscribeAsync(sid, uid, "not-a-number-legacy-id", cts2.Token))
            {
                replayed.Add(evt);
            }
        }
        catch (OperationCanceledException) { }
        catch
        {
            threwException = true;
        }

        // Assert: no exception thrown; replay may be empty (in-memory fallback for unrecognised id)
        threwException.Should().BeFalse("garbage lastEventId must never throw — graceful fallback expected");
    }

    /// <summary>
    /// PRIVACY REGRESSION: a private event published for userA must NOT be delivered to userB
    /// when userB reconnects via the Redis ZSET cross-instance replay path.
    /// userA's own reconnect MUST receive the private event.
    ///
    /// Regression for the Critical bug fixed in Issue #2561 SP2 T7:
    /// the original ZSET path stored bare envelopes (no visibility) → leaked private events to all
    /// reconnecting subscribers. Fix: <see cref="ReplayEntry"/> wrapper encodes visibility at write
    /// time; filter applied at read time in GetReplayFromRedisAsync.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("BoundedContext", "SessionTracking")]
    public async Task Redis_PrivateEvent_NotLeakedToOtherSubscriber_OnCrossInstanceReplay()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        if (fixture is null)
        {
            // Docker not available — observable skip (NOT a silent no-op pass) so a green run
            // never masks an unexecuted regression test.
            Assert.Skip("Docker/Redis unavailable — Testcontainers fixture could not start.");
            return;
        }

        var sid = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        // Instance A publishes the sequence below.
        using var svcA = CreateRedisService(fixture.Multiplexer);

        // Keep-alive subscriber so the pool exists while publishing
        using var keepAliveCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var keepAlive = Task.Run(async () =>
        {
            try { await foreach (var _ in svcA.SubscribeAsync(sid, Guid.NewGuid(), null, keepAliveCts.Token)) { } }
            catch (OperationCanceledException) { }
        }, keepAliveCts.Token);
        await Task.Delay(50);

        // Capture the id of the FIRST event (so we can reconnect "since" it)
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var capturedIds = new List<string>();
        var captureTask = Task.Run(async () =>
        {
            await foreach (var evt in svcA.SubscribeAsync(sid, userA, null, cts1.Token))
            {
                capturedIds.Add(evt.Id);
                if (capturedIds.Count >= 1) break;
            }
        }, cts1.Token);
        await Task.Delay(50);

        // Event 1: explicit public (anchor — replayed events start AFTER this one)
        await svcA.PublishAsync(sid, MakeScoreEvent(sid, Guid.NewGuid()), EventVisibility.Public);
        await captureTask; // wait until we have the first event id

        // Event 2: private — only for userA (must NOT leak to userB)
        await svcA.PublishAsync(sid, MakeScoreEvent(sid, userA), EventVisibility.PrivateTo(userA));
        // Event 3: explicit public again (both users must receive)
        await svcA.PublishAsync(sid, MakeScoreEvent(sid, Guid.NewGuid()), EventVisibility.Public);
        // Event 4: default(EventVisibility) → TargetUserId=null, IsPublic=false → broadcast-to-all.
        // This is the dominant LiveSessionStreamGateway path. Both users MUST receive it; it must
        // NOT be dropped by a too-aggressive filter missing the TargetUserId.HasValue guard.
        await svcA.PublishAsync(sid, MakeScoreEvent(sid, Guid.NewGuid()), default);

        // Poll until all 4 published events have settled into the replay ZSET, instead of a fixed
        // Task.Delay(50) which races on a slow/loaded CI runner — hardens the privacy gate (#2565).
        var replayKey = Api.SharedKernel.Constants.RedisKeyConstants.GetSessionReplayKey(sid);
        await WaitForZsetCardinalityAsync(fixture.Multiplexer.GetDatabase(), replayKey, expected: 4);

        await keepAliveCts.CancelAsync();
        try { await keepAlive; } catch (OperationCanceledException) { }

        var anchorId = capturedIds[0]; // reconnect "since" event-1 → should replay events 2,3,4

        // Instance B: reconnect as userB since the anchor → must see events 3 (public) + 4 (broadcast),
        // but NOT event 2 (private to userA).
        using var svcB = CreateRedisService(fixture.Multiplexer);

        using var ctsBReconnect = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var userBReplayed = new List<SseEventEnvelope>();
        try
        {
            await foreach (var evt in svcB.SubscribeAsync(sid, userB, anchorId, ctsBReconnect.Token))
            {
                userBReplayed.Add(evt);
                if (userBReplayed.Count >= 5) break; // safety cap
            }
        }
        catch (OperationCanceledException) { }

        // Instance C: reconnect as userA since the anchor → must see ALL of events 2,3,4.
        using var svcC = CreateRedisService(fixture.Multiplexer);

        using var ctsCReconnect = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var userAReplayed = new List<SseEventEnvelope>();
        try
        {
            await foreach (var evt in svcC.SubscribeAsync(sid, userA, anchorId, ctsCReconnect.Token))
            {
                userAReplayed.Add(evt);
                if (userAReplayed.Count >= 5) break; // safety cap
            }
        }
        catch (OperationCanceledException) { }

        // Assert: userB sees event 3 (public) + event 4 (broadcast-to-all) = 2 events,
        // NOT the private event (event 2).
        userBReplayed.Should().HaveCount(2,
            because: "userB must receive the explicit-public AND the broadcast-to-all (null-target) events, but never the private one");

        // Assert: userA sees event 2 (private to userA) + event 3 (public) + event 4 (broadcast) = 3 events.
        userAReplayed.Should().HaveCount(3,
            because: "userA must receive the private event addressed to them, the explicit-public, and the broadcast-to-all event");
    }

    #endregion

    // =========================================================================
    // Helpers
    // =========================================================================

    private static SessionBroadcastService CreateInMemoryService() =>
        new(
            NullLogger<SessionBroadcastService>.Instance,
            new RedisSessionSequenceProvider(null, NullLogger<RedisSessionSequenceProvider>.Instance));

    private static SessionBroadcastService CreateRedisService(IConnectionMultiplexer redis) =>
        new(
            NullLogger<SessionBroadcastService>.Instance,
            new RedisSessionSequenceProvider(redis, NullLogger<RedisSessionSequenceProvider>.Instance),
            redis);

    private static ScoreUpdatedEvent MakeScoreEvent(Guid sessionId, Guid userId) =>
        new()
        {
            SessionId = sessionId,
            ParticipantId = userId,
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 10
        };

    /// <summary>
    /// Polls the replay ZSET until it holds at least <paramref name="expected"/> entries, replacing a
    /// fixed Task.Delay that races on slow CI runners. Returns when the cardinality is reached or the
    /// 2s budget elapses — in which case the test's own replay assertions surface the shortfall.
    /// </summary>
    private static async Task WaitForZsetCardinalityAsync(IDatabase db, RedisKey key, long expected)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!cts.IsCancellationRequested)
        {
            if (await db.SortedSetLengthAsync(key).ConfigureAwait(false) >= expected)
            {
                return;
            }

            try { await Task.Delay(10, cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    // =========================================================================
    // Testcontainers fixture — isolated Redis per test class
    // =========================================================================

    /// <summary>
    /// Lightweight per-test Redis fixture using Testcontainers.Redis.
    /// Returns null when Docker is not available, allowing tests to skip gracefully.
    /// </summary>
    private sealed class RedisFixture : IAsyncDisposable
    {
        private readonly RedisContainer _container;
        public IConnectionMultiplexer Multiplexer { get; }

        private RedisFixture(RedisContainer container, IConnectionMultiplexer mux)
        {
            _container = container;
            Multiplexer = mux;
        }

        public static async Task<RedisFixture?> CreateAsync()
        {
            try
            {
                var container = new RedisBuilder()
                    .WithImage("redis:7-alpine")
                    .Build();

                await container.StartAsync();

                var mux = await ConnectionMultiplexer.ConnectAsync(
                    container.GetConnectionString()).ConfigureAwait(false);

                return new RedisFixture(container, mux);
            }
            catch (Exception)
            {
                // Docker not available (CI shard without Docker, local without daemon, etc.)
                // Tests using this fixture will skip gracefully via early return.
                return null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Multiplexer.DisposeAsync();
            await _container.DisposeAsync();
        }
    }
}
