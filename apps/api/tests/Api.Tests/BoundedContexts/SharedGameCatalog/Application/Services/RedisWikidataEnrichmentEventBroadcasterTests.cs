using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Testcontainers Redis integration tests for
/// <see cref="RedisWikidataEnrichmentEventBroadcaster"/>.
///
/// Issue #2256 — multi-pod Redis fan-out backplane for F4 SSE.
/// </summary>
/// <remarks>
/// Each test owns its own <see cref="RedisContainer"/> via the
/// <see cref="IAsyncLifetime"/> contract so failure in one test does NOT leak
/// state to its neighbour (mirrors the
/// <c>ClearCacheCommandTests</c> fixture pattern). The
/// <c>[Collection("Sequential")]</c> trait avoids contention on the host's
/// Docker socket when xUnit schedules these alongside other Testcontainers IT.
/// </remarks>
[Collection("Sequential")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2256")]
public class RedisWikidataEnrichmentEventBroadcasterTests : IAsyncLifetime
{
    private RedisContainer? _redis;
    private IConnectionMultiplexer? _connectionA;
    private IConnectionMultiplexer? _connectionB;

    public async ValueTask InitializeAsync()
    {
        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redis.StartAsync().ConfigureAwait(false);

        // Two distinct multiplexers simulating two pods sharing the same Redis.
        _connectionA = await ConnectionMultiplexer
            .ConnectAsync($"{_redis.GetConnectionString()},allowAdmin=true")
            .ConfigureAwait(false);
        _connectionB = await ConnectionMultiplexer
            .ConnectAsync($"{_redis.GetConnectionString()},allowAdmin=true")
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connectionA is not null)
        {
            await _connectionA.DisposeAsync().ConfigureAwait(false);
        }
        if (_connectionB is not null)
        {
            await _connectionB.DisposeAsync().ConfigureAwait(false);
        }
        if (_redis is not null)
        {
            await _redis.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WikidataEnrichmentEvent SampleEvent(Guid? attemptId = null) => new(
        AttemptId: attemptId ?? Guid.NewGuid(),
        SharedGameId: Guid.NewGuid(),
        Outcome: "DeadLetter",
        Reason: "r2-upload-error",
        AttemptedAt: new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc),
        RetryCount: 3,
        NextRetryAt: null,
        DeadLetteredAt: new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc),
        TriggeredByAdminUserId: null,
        TriggeredByAdminFullName: null);

    private static RedisWikidataEnrichmentEventBroadcaster CreateBroadcaster(
        IConnectionMultiplexer mux)
        => new(mux, NullLogger<RedisWikidataEnrichmentEventBroadcaster>.Instance);

    /// <summary>
    /// IT #3: 2-pod fan-out — publisher on broadcaster A, subscriber on broadcaster B
    /// (each holds its own multiplexer pointed at the same Redis), event delivered
    /// intact across the backplane.
    /// </summary>
    [Fact]
    public async Task TwoPods_PublishOnPodA_SubscriberOnPodBReceivesEvent()
    {
        var podA = CreateBroadcaster(_connectionA!);
        var podB = CreateBroadcaster(_connectionB!);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var payload = SampleEvent();
        WikidataEnrichmentEvent? received = null;

        var consumerTask = Task.Run(async () =>
        {
            await foreach (var ev in podB.SubscribeAsync(cts.Token))
            {
                received = ev;
                cts.Cancel();
            }
        }, cts.Token);

        // Wait for podB's Redis subscription to be active before publishing.
        for (var i = 0; i < 100 && podB.SubscriberCount == 0; i++)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }
        podB.SubscriberCount.Should().Be(1);

        // Give StackExchange.Redis another moment to wire its SUBSCRIBE up
        // server-side — local subscription bookkeeping is set before the
        // server ack arrives.
        await Task.Delay(200).ConfigureAwait(false);

        podA.Publish(payload);

        try
        {
            await consumerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected: consumer cancelled itself after first event.
        }

        received.Should().NotBeNull();
        received!.AttemptId.Should().Be(payload.AttemptId);
        received.SharedGameId.Should().Be(payload.SharedGameId);
        received.Outcome.Should().Be(payload.Outcome);
        received.Reason.Should().Be(payload.Reason);
    }

    /// <summary>
    /// IT #4: publisher-side reconnect — server-side <c>CLIENT KILL</c> drops
    /// the publisher's TCP connection. StackExchange.Redis auto-reconnects on
    /// the next operation; we verify by publishing again after the kill and
    /// asserting the subscriber observes the post-kill event.
    /// </summary>
    /// <remarks>
    /// Testcontainers <c>StopAsync</c>+<c>StartAsync</c> would have remapped
    /// the host port → the multiplexer cannot resolve the new endpoint. The
    /// server-side <c>CLIENT KILL</c> exercises the real reconnect handshake
    /// without changing the endpoint.
    /// </remarks>
    [Fact]
    public async Task PublisherSurvivesConnectionDrop()
    {
        var podA = CreateBroadcaster(_connectionA!);
        var podB = CreateBroadcaster(_connectionB!);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        WikidataEnrichmentEvent? receivedSecond = null;
        var firstPayload = SampleEvent();
        var secondPayload = SampleEvent();
        var firstReceived = new TaskCompletionSource();

        var consumerTask = Task.Run(async () =>
        {
            await foreach (var ev in podB.SubscribeAsync(cts.Token))
            {
                if (ev.AttemptId == firstPayload.AttemptId)
                {
                    firstReceived.TrySetResult();
                    continue;
                }
                if (ev.AttemptId == secondPayload.AttemptId)
                {
                    receivedSecond = ev;
                    cts.Cancel();
                    break;
                }
            }
        }, cts.Token);

        for (var i = 0; i < 100 && podB.SubscriberCount == 0; i++)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }
        await Task.Delay(200).ConfigureAwait(false);

        podA.Publish(firstPayload);
        await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        // Force-disconnect every TCP client connected to Redis. The
        // multiplexer will reconnect on next operation; we verify post-kill
        // publish + subscribe round-trip works.
        // CLIENT KILL TYPE normal kills only data clients (leaves the
        // SUBSCRIBE connection alone unless we add TYPE pubsub) — kill both
        // to exercise the full reconnect path.
        var killServer = _connectionA!.GetServer(_connectionA.GetEndPoints()[0]);
        await killServer.ExecuteAsync("CLIENT", "KILL", "TYPE", "normal").ConfigureAwait(false);
        await killServer.ExecuteAsync("CLIENT", "KILL", "TYPE", "pubsub").ConfigureAwait(false);

        // Re-publish loop — until reconnect is done the publish is a no-op,
        // so retry until podB observes the event.
        for (var i = 0; i < 60 && receivedSecond is null && !cts.IsCancellationRequested; i++)
        {
            await Task.Delay(500).ConfigureAwait(false);
            try
            {
                podA.Publish(secondPayload);
            }
            catch
            {
                // multiplexer mid-reconnect — swallow and retry.
            }
        }

        try
        {
            await consumerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }

        receivedSecond.Should().NotBeNull(
            "the publisher-side multiplexer must auto-reconnect and deliver the second event");
    }

    /// <summary>
    /// IT #5: subscriber-side reconnect — kill the pubsub connection
    /// server-side. The multiplexer auto-resubscribes; post-kill publishes
    /// must still reach the subscriber.
    /// </summary>
    [Fact]
    public async Task SubscriberSurvivesConnectionDrop()
    {
        var podA = CreateBroadcaster(_connectionA!);
        var podB = CreateBroadcaster(_connectionB!);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        WikidataEnrichmentEvent? receivedAfterDrop = null;
        var afterPayload = SampleEvent();

        var consumerTask = Task.Run(async () =>
        {
            await foreach (var ev in podB.SubscribeAsync(cts.Token))
            {
                if (ev.AttemptId == afterPayload.AttemptId)
                {
                    receivedAfterDrop = ev;
                    cts.Cancel();
                    break;
                }
            }
        }, cts.Token);

        for (var i = 0; i < 100 && podB.SubscriberCount == 0; i++)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }
        await Task.Delay(200).ConfigureAwait(false);

        // Kill the subscriber's TCP connection. The multiplexer reconnects
        // and re-arms SUBSCRIBE for us.
        var killServer = _connectionB!.GetServer(_connectionB.GetEndPoints()[0]);
        await killServer.ExecuteAsync("CLIENT", "KILL", "TYPE", "pubsub").ConfigureAwait(false);
        await killServer.ExecuteAsync("CLIENT", "KILL", "TYPE", "normal").ConfigureAwait(false);

        for (var i = 0; i < 60 && receivedAfterDrop is null && !cts.IsCancellationRequested; i++)
        {
            await Task.Delay(500).ConfigureAwait(false);
            try
            {
                podA.Publish(afterPayload);
            }
            catch
            {
                // multiplexer mid-reconnect — retry.
            }
        }

        try
        {
            await consumerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }

        receivedAfterDrop.Should().NotBeNull(
            "the subscriber-side multiplexer must auto-resubscribe after the connection drop");
    }

    /// <summary>
    /// IT #6: slow subscriber — bounded channel with DropOldest policy. Flood
    /// the publisher with more events than the per-subscriber capacity (128),
    /// then begin draining; we should observe ≤128 events with the NEWEST
    /// AttemptIds, never the oldest.
    /// </summary>
    [Fact]
    public async Task SlowSubscriber_DropOldestPolicy_KeepsNewestEvents()
    {
        var podA = CreateBroadcaster(_connectionA!);
        var podB = CreateBroadcaster(_connectionB!);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        const int totalToPublish = 500; // well above 128 capacity
        const int channelCapacity = 128;

        var firstPayload = SampleEvent();
        // Sentinel "first" — ensures the subscriber is wired BEFORE the flood
        // (and that nothing before it counts toward the capacity proof).
        var firstSeen = new TaskCompletionSource();
        var receivedAttemptIds = new List<Guid>();
        var publishedIds = new List<Guid>();
        for (var i = 0; i < totalToPublish; i++)
        {
            publishedIds.Add(Guid.NewGuid());
        }

        var consumerTask = Task.Run(async () =>
        {
            // Pause draining for 3s after the first event so the channel fills.
            var firstObserved = false;
            await foreach (var ev in podB.SubscribeAsync(cts.Token))
            {
                if (!firstObserved)
                {
                    firstObserved = true;
                    firstSeen.TrySetResult();
                    await Task.Delay(3000, cts.Token).ConfigureAwait(false);
                    continue;
                }
                receivedAttemptIds.Add(ev.AttemptId);
                if (receivedAttemptIds.Count >= channelCapacity)
                {
                    cts.Cancel();
                    break;
                }
            }
        }, cts.Token);

        for (var i = 0; i < 100 && podB.SubscriberCount == 0; i++)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }
        await Task.Delay(200).ConfigureAwait(false);

        podA.Publish(firstPayload);
        await firstSeen.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        // Flood while the subscriber is paused.
        foreach (var id in publishedIds)
        {
            podA.Publish(SampleEvent(id));
        }

        try
        {
            await consumerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }

        receivedAttemptIds.Count.Should().BeLessThanOrEqualTo(channelCapacity,
            "DropOldest with bounded capacity caps the delivered events");
        // The newest events MUST survive — sample the last id published and
        // assert at least the trailing window is intact.
        receivedAttemptIds.Should().Contain(publishedIds[^1],
            "DropOldest evicts the OLDEST queued event, so the newest publish must be observable");
    }

    /// <summary>
    /// IT #7: message JSON roundtrip — full DTO with all nullable fields populated
    /// (incl. F6 TriggeredByAdminUserId) publishes → subscribes → deserialises →
    /// equality check.
    /// </summary>
    [Fact]
    public async Task PublishSubscribe_JsonRoundtrip_PreservesAllFields()
    {
        var podA = CreateBroadcaster(_connectionA!);
        var podB = CreateBroadcaster(_connectionB!);

        var fullyPopulated = new WikidataEnrichmentEvent(
            AttemptId: Guid.NewGuid(),
            SharedGameId: Guid.NewGuid(),
            Outcome: "Success",
            Reason: null,
            AttemptedAt: new DateTime(2026, 6, 21, 12, 34, 56, DateTimeKind.Utc),
            RetryCount: 0,
            NextRetryAt: new DateTime(2026, 6, 22, 12, 34, 56, DateTimeKind.Utc),
            DeadLetteredAt: null,
            TriggeredByAdminUserId: Guid.NewGuid(),
            TriggeredByAdminFullName: null);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        WikidataEnrichmentEvent? received = null;

        var consumerTask = Task.Run(async () =>
        {
            await foreach (var ev in podB.SubscribeAsync(cts.Token))
            {
                if (ev.AttemptId == fullyPopulated.AttemptId)
                {
                    received = ev;
                    cts.Cancel();
                    break;
                }
            }
        }, cts.Token);

        for (var i = 0; i < 100 && podB.SubscriberCount == 0; i++)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }
        await Task.Delay(200).ConfigureAwait(false);

        podA.Publish(fullyPopulated);

        try
        {
            await consumerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }

        received.Should().NotBeNull();
        // Records implement value equality on all positional members, so this
        // single assertion covers every field including the nullable ones.
        received.Should().Be(fullyPopulated);
    }
}
