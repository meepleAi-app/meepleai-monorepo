using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Infrastructure.EventBroadcasting;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.EventBroadcasting;

/// <summary>
/// Unit tests for <see cref="ChannelEventBroadcaster"/> — Task 1.3 of
/// F4.1 issue #1718 (SP5 Admin Console — A8 Monitor + LiveEventLog).
///
/// Six cases verifying: publish/subscribe, async enumerable type, fan-out,
/// backpressure (DropOldest + drop counter), cancellation cleanup, and disposal.
/// All in-process — no DB, no network.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1718")]
public sealed class ChannelEventBroadcasterTests : IDisposable
{
    private readonly ChannelEventBroadcaster _sut;

    public ChannelEventBroadcasterTests()
    {
        _sut = new ChannelEventBroadcaster();
    }

    public void Dispose() => _sut.Dispose();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DomainEventDto MakeEvent(
        string eventType = "agent.created",
        string? aggregateType = "Agent",
        Guid? aggregateId = null,
        Guid? userId = null)
        => new(
            Id: Guid.NewGuid(),
            EventId: Guid.NewGuid(),
            EventType: eventType,
            AggregateType: aggregateType,
            AggregateId: aggregateId ?? Guid.NewGuid(),
            UserId: userId ?? Guid.NewGuid(),
            PayloadJson: "{}",
            PayloadVersion: 1,
            OccurredAt: DateTime.UtcNow,
            LoggedAt: DateTime.UtcNow);

    private static EventBroadcastFilter NoFilter() => new(); // all nulls = match everything

    /// <summary>
    /// Reads the very first item from an async enumerable and returns it.
    /// Times out (throws <see cref="OperationCanceledException"/>) if no item arrives
    /// before <paramref name="ct"/> is cancelled.
    /// </summary>
    private static async Task<T> FirstAsync<T>(IAsyncEnumerable<T> source, CancellationToken ct)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            return item;
        }
        throw new InvalidOperationException("Sequence contained no elements before cancellation.");
    }

    /// <summary>
    /// Reads up to <paramref name="count"/> items from an async enumerable.
    /// </summary>
    private static async Task<List<T>> TakeAsync<T>(IAsyncEnumerable<T> source, int count, CancellationToken ct)
    {
        var result = new List<T>(count);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            result.Add(item);
            if (result.Count >= count) break;
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Test 1: Publish → event is available to subscriber
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Publish_QueuesEvent_AvailableToSubscribers()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var evt = MakeEvent("agent.created");

        // Subscribe first, then publish
        var enumerable = _sut.Subscribe(NoFilter(), cts.Token);
        _sut.Publish(evt);

        // Assert — the first item yielded should be our event
        var received = await FirstAsync(enumerable, cts.Token);
        received.Should().BeEquivalentTo(evt);
    }

    // -------------------------------------------------------------------------
    // Test 2: Subscribe returns IAsyncEnumerable<DomainEventDto>
    // -------------------------------------------------------------------------

    [Fact]
    public void Subscribe_ReturnsAsyncEnumerable()
    {
        // Act
        var result = _sut.Subscribe(NoFilter(), CancellationToken.None);

        // Assert — must be a valid IAsyncEnumerable (not null, correct type)
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IAsyncEnumerable<DomainEventDto>>();
    }

    // -------------------------------------------------------------------------
    // Test 3: Multiple subscribers all receive the same event (fan-out)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Multiple_Subscribers_AllReceiveEvents()
    {
        // Arrange — spin up three subscribers before publishing
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var sub1 = _sut.Subscribe(NoFilter(), cts.Token);
        var sub2 = _sut.Subscribe(NoFilter(), cts.Token);
        var sub3 = _sut.Subscribe(NoFilter(), cts.Token);

        var evt = MakeEvent("kb.doc.indexed");

        // Act
        _sut.Publish(evt);

        // Assert — all three receive the same event
        var r1 = await FirstAsync(sub1, cts.Token);
        var r2 = await FirstAsync(sub2, cts.Token);
        var r3 = await FirstAsync(sub3, cts.Token);

        r1.EventId.Should().Be(evt.EventId);
        r2.EventId.Should().Be(evt.EventId);
        r3.EventId.Should().Be(evt.EventId);
    }

    // -------------------------------------------------------------------------
    // Test 4: Backpressure — DropOldest; DroppedCount increments when full
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Backpressure_DropOldest_DroppedCountIncrements()
    {
        // Arrange — tiny-capacity broadcaster so we can fill it cheaply
        using var smallBroadcaster = new ChannelEventBroadcaster(capacity: 3);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Subscribe but do NOT consume — keeps the channel full
        var enumerable = smallBroadcaster.Subscribe(NoFilter(), cts.Token);

        // Fill all 3 slots
        smallBroadcaster.Publish(MakeEvent("session.created"));   // slot 0
        smallBroadcaster.Publish(MakeEvent("session.created"));   // slot 1
        smallBroadcaster.Publish(MakeEvent("session.created"));   // slot 2

        // This publish triggers DropOldest: oldest item is ejected, counter increments
        smallBroadcaster.Publish(MakeEvent("session.finalized")); // overflow → drop

        // Assert — DroppedEvents counter has been incremented at least once
        smallBroadcaster.DroppedEvents.Should().BeGreaterThanOrEqualTo(1);

        // Drain the channel: 3 items should still be readable (the 3 newest)
        var items = await TakeAsync(enumerable, 3, cts.Token);
        items.Should().HaveCount(3);
    }

    // -------------------------------------------------------------------------
    // Test 5: Cancellation removes subscriber — subsequent publishes not delivered
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Subscribe_AfterUnsubscribe_NoDelivery()
    {
        // Arrange
        using var subscribeCts = new CancellationTokenSource();
        var events = new List<DomainEventDto>();

        // Subscribe and start consuming in background
        var consumerTask = Task.Run(async () =>
        {
            await foreach (var e in _sut.Subscribe(NoFilter(), subscribeCts.Token).ConfigureAwait(false))
            {
                events.Add(e);
            }
        });

        // Publish one event so we know the subscriber is active
        _sut.Publish(MakeEvent("agent.created"));
        await Task.Delay(50); // let consumer pick it up

        // Cancel (simulate unsubscribe)
        await subscribeCts.CancelAsync();

        // Wait for consumer to exit cleanly (up to 1 s)
        await Task.WhenAny(consumerTask, Task.Delay(1000));

        // Snapshot count after cancellation
        var snapshot = events.Count;

        // Publish after cancellation — should NOT be delivered
        _sut.Publish(MakeEvent("session.created"));
        await Task.Delay(50); // give time for any spurious delivery

        // Assert — no new events delivered after cancellation
        events.Count.Should().Be(snapshot);
    }

    // -------------------------------------------------------------------------
    // Test 6: Dispose closes all channels (async loop exits normally)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Dispose_ClosesAllChannels()
    {
        // Arrange — separate broadcaster so we can dispose independently
        var broadcaster = new ChannelEventBroadcaster();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // IMPORTANT: Subscribe on the calling thread so channel is registered
        // before Publish below. SubscribeCore is lazy; Subscribe itself is eager.
        var enumerable = broadcaster.Subscribe(NoFilter(), cts.Token);

        var receivedCount = 0;
        var consumerTask = Task.Run(async () =>
        {
            // ReadAllAsync exits normally when the writer is completed (disposed).
            await foreach (var _ in enumerable.ConfigureAwait(false))
            {
                receivedCount++;
            }
        });

        // Publish one event to confirm the channel is live
        broadcaster.Publish(MakeEvent("agent.created"));
        await Task.Delay(50); // let consumer pick it up

        // Act — dispose closes all writers, triggering loop completion
        broadcaster.Dispose();

        // Assert — consumer task finishes (writer closed = ReadAllAsync completes)
        var finished = await Task.WhenAny(consumerTask, Task.Delay(2000)) == consumerTask;
        finished.Should().BeTrue("consumer should complete when broadcaster is disposed");
        receivedCount.Should().BeGreaterThanOrEqualTo(1);
    }
}
