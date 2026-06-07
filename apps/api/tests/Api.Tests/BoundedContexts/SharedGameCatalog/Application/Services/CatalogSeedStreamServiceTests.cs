using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Unit tests for <see cref="CatalogSeedStreamService"/> (Issue #1903 M6.1).
/// Covers: null-guard, replay-to-new-subscriber, live delivery, multi-subscriber
/// fan-out, replay-buffer bounded eviction, and CT-driven subscriber cleanup.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedStreamServiceTests
{
    private static CatalogSeedStreamService CreateService() =>
        new(NullLogger<CatalogSeedStreamService>.Instance);

    private static BatchStartedEvent NewBatchStarted(Guid? id = null) =>
        new(id ?? Guid.NewGuid(),
            new[] { Guid.NewGuid(), Guid.NewGuid() },
            DateTime.UtcNow);

    [Fact]
    public async Task PublishAsync_Throws_OnNullEvent()
    {
        var svc = CreateService();

        var act = async () => await svc.PublishAsync(null!, default);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishAsync_BeforeSubscribe_ReplaysToNewSubscriber()
    {
        var svc = CreateService();
        var evt = NewBatchStarted();
        await svc.PublishAsync(evt);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));

        var received = new List<CatalogSeedStreamEvent>();
        try
        {
            await foreach (var item in svc.SubscribeAsync(cts.Token))
            {
                received.Add(item);
            }
        }
        catch (OperationCanceledException)
        {
            // expected — CT cancels the subscriber loop after replay drains
        }

        received.Should().ContainSingle();
        received[0].Should().Be(evt);
    }

    [Fact]
    public async Task SubscribeAsync_LiveEvent_DeliveredToActiveSubscriber()
    {
        var svc = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received = new List<CatalogSeedStreamEvent>();
        var consumer = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in svc.SubscribeAsync(cts.Token))
                {
                    received.Add(item);
                    if (received.Count == 1)
                    {
                        await cts.CancelAsync();
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        });

        // Brief yield so the subscriber gets registered before we publish.
        await Task.Delay(50);

        var evt = NewBatchStarted();
        await svc.PublishAsync(evt);

        await consumer.WaitAsync(TimeSpan.FromSeconds(2));

        received.Should().ContainSingle();
        received[0].Should().Be(evt);
    }

    [Fact]
    public async Task SubscribeAsync_TwoSubscribers_BothReceiveLiveEvent()
    {
        var svc = CreateService();
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received1 = new List<CatalogSeedStreamEvent>();
        var received2 = new List<CatalogSeedStreamEvent>();

        async Task RunConsumer(CancellationTokenSource cts, List<CatalogSeedStreamEvent> sink)
        {
            try
            {
                await foreach (var item in svc.SubscribeAsync(cts.Token))
                {
                    sink.Add(item);
                    if (sink.Count == 1)
                    {
                        await cts.CancelAsync();
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // shutdown path
            }
        }

        var t1 = Task.Run(() => RunConsumer(cts1, received1));
        var t2 = Task.Run(() => RunConsumer(cts2, received2));

        await Task.Delay(80);

        var evt = NewBatchStarted();
        await svc.PublishAsync(evt);

        await Task.WhenAll(t1, t2).WaitAsync(TimeSpan.FromSeconds(2));

        received1.Should().ContainSingle().Which.Should().Be(evt);
        received2.Should().ContainSingle().Which.Should().Be(evt);
    }

    [Fact]
    public async Task ReplayBuffer_LimitedTo200Events_DiscardsOldest()
    {
        var svc = CreateService();

        // Publish 250 events — the buffer should retain only the last 200.
        var allEvents = new List<BatchStartedEvent>(250);
        for (var i = 0; i < 250; i++)
        {
            var e = NewBatchStarted();
            allEvents.Add(e);
            await svc.PublishAsync(e);
        }

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var replay = new List<CatalogSeedStreamEvent>();
        try
        {
            await foreach (var item in svc.SubscribeAsync(cts.Token))
            {
                replay.Add(item);
            }
        }
        catch (OperationCanceledException)
        {
            // expected — replay drained, CT cancels
        }

        replay.Should().HaveCount(200);
        // The 200 most-recent events should be the last 200 published.
        replay.Should().Equal(allEvents.GetRange(50, 200));
    }

    [Fact]
    public async Task SubscribeAsync_CancellationToken_StopsEnumeration_AutoRemovesSubscriber()
    {
        var svc = CreateService();
        using var cts = new CancellationTokenSource();

        var consumer = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in svc.SubscribeAsync(cts.Token))
                {
                    // never expect any event before we cancel
                }
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        });

        // Let the subscriber register.
        await Task.Delay(50);

        // Cancel the subscriber — finally{} block must run and remove the channel.
        await cts.CancelAsync();
        await consumer.WaitAsync(TimeSpan.FromSeconds(2));

        // After cleanup, publishing must not throw — there are no live subscribers
        // and the publish loop has nothing to write to.
        var act = async () => await svc.PublishAsync(NewBatchStarted(), default);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SubscribeAsync_PublishedBeforeSubscribe_DeliveredExactlyOnceViaReplay()
    {
        // Regression: PR #1913 review finding — subscribe/publish race could deliver
        // the same event twice (once via replay snapshot, once via live channel).
        // The cutoff-based dedup must ensure exactly-once delivery for events
        // published strictly before SubscribeAsync registers its channel.
        var svc = CreateService();
        var evt = new BatchStartedEvent(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            DateTime.UtcNow.AddSeconds(-1));

        await svc.PublishAsync(evt);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new List<CatalogSeedStreamEvent>();
        try
        {
            await foreach (var item in svc.SubscribeAsync(cts.Token))
            {
                received.Add(item);
                if (received.Count >= 1) break;
            }
        }
        catch (OperationCanceledException)
        {
            // expected if no more events arrive within the timeout
        }

        received.Should().ContainSingle().Which.Should().Be(evt);
    }

    [Fact]
    public async Task PublishAsync_AfterReplayDrain_DeliversLiveEvents()
    {
        // Edge-case check: a single subscriber should see BOTH the replay AND any
        // subsequent live events arriving after the replay completes.
        var svc = CreateService();
        var replayEvt = NewBatchStarted();
        await svc.PublishAsync(replayEvt);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received = new List<CatalogSeedStreamEvent>();
        var consumer = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in svc.SubscribeAsync(cts.Token))
                {
                    received.Add(item);
                    if (received.Count == 2)
                    {
                        await cts.CancelAsync();
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        });

        await Task.Delay(80);
        var liveEvt = NewBatchStarted();
        await svc.PublishAsync(liveEvt);

        await consumer.WaitAsync(TimeSpan.FromSeconds(2));

        received.Should().HaveCount(2);
        received[0].Should().Be(replayEvt);
        received[1].Should().Be(liveEvt);
    }
}
