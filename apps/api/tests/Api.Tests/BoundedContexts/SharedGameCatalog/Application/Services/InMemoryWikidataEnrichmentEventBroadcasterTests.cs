using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Unit tests for <see cref="InMemoryWikidataEnrichmentEventBroadcaster"/>.
/// Issue #1823 Phase E F4.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class InMemoryWikidataEnrichmentEventBroadcasterTests
{
    private static WikidataEnrichmentEvent SampleEvent(Guid? attemptId = null) => new(
        AttemptId: attemptId ?? Guid.NewGuid(),
        SharedGameId: Guid.NewGuid(),
        Outcome: "DeadLetter",
        Reason: "r2-upload-error",
        AttemptedAt: new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc),
        RetryCount: 3,
        NextRetryAt: null,
        DeadLetteredAt: new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc),
        // F6 #1823 Phase F: broadcaster fan-out exercise is trigger-source
        // agnostic; sample defaults to scheduler-authored (null).
        TriggeredByAdminUserId: null,
        TriggeredByAdminFullName: null);

    private static InMemoryWikidataEnrichmentEventBroadcaster CreateSut() => new(
        NullLogger<InMemoryWikidataEnrichmentEventBroadcaster>.Instance);

    [Fact]
    public void SubscriberCount_StartsAtZero()
    {
        CreateSut().SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void Publish_NoSubscribers_DoesNotThrow()
    {
        var sut = CreateSut();
        var act = () => sut.Publish(SampleEvent());
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Publish_OneSubscriber_DeliversPayload()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var payload = SampleEvent();
        var subscribed = new TaskCompletionSource();
        WikidataEnrichmentEvent? received = null;

        var consumerTask = Task.Run(async () =>
        {
            await foreach (var ev in sut.SubscribeAsync(cts.Token))
            {
                received = ev;
                cts.Cancel();
            }
        }, cts.Token);

        // Wait until the subscriber registers — the SubscriberCount counter is
        // the cheapest signal we have. Yield briefly so the consumer task runs.
        for (var i = 0; i < 50 && sut.SubscriberCount == 0; i++)
        {
            await Task.Delay(10);
        }

        sut.SubscriberCount.Should().Be(1);
        sut.Publish(payload);

        try
        {
            await consumerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected: consumer cancelled itself after receiving the payload.
        }

        received.Should().BeEquivalentTo(payload);
        sut.SubscriberCount.Should().Be(0, "the consumer's finally MUST remove the subscriber on exit");
    }

    [Fact]
    public async Task Publish_MultipleSubscribers_FanOutsToAll()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var payload = SampleEvent();
        var receivedA = 0;
        var receivedB = 0;

        var taskA = Task.Run(async () =>
        {
            await foreach (var _ in sut.SubscribeAsync(cts.Token))
            {
                Interlocked.Increment(ref receivedA);
                break;
            }
        }, cts.Token);

        var taskB = Task.Run(async () =>
        {
            await foreach (var _ in sut.SubscribeAsync(cts.Token))
            {
                Interlocked.Increment(ref receivedB);
                break;
            }
        }, cts.Token);

        for (var i = 0; i < 50 && sut.SubscriberCount < 2; i++)
        {
            await Task.Delay(10);
        }
        sut.SubscriberCount.Should().Be(2);

        sut.Publish(payload);

        await Task.WhenAll(taskA, taskB).WaitAsync(TimeSpan.FromSeconds(5));

        receivedA.Should().Be(1);
        receivedB.Should().Be(1);
    }

    [Fact]
    public async Task SubscribeAsync_CancellationCleansUpSubscriber()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        var consumerTask = Task.Run(async () =>
        {
            await foreach (var _ in sut.SubscribeAsync(cts.Token))
            {
                // Never receives anything — cancel below kicks it out.
            }
        }, cts.Token);

        for (var i = 0; i < 50 && sut.SubscriberCount == 0; i++)
        {
            await Task.Delay(10);
        }
        sut.SubscriberCount.Should().Be(1);

        cts.Cancel();
        try
        {
            await consumerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }

        sut.SubscriberCount.Should().Be(0,
            "the consumer's finally block MUST remove the subscriber on cancellation");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Issue #2470 — instrumentation: every Publish ticks the published
    // counter once; the received counter ticks by the snapshot subscriber
    // count at the time of publish. Tests use a MeterListener to capture
    // raw measurements off the shared global Meter (mirror of the
    // WikidataEnrichmentMetricsTests pattern).
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Publish_AlwaysIncrementsPublishedCounter_ByOne()
    {
        var sut = CreateSut();

        var delta = MeasureCounterDelta(
            "meepleai.wikidata.sse.messages.published.total",
            () => sut.Publish(SampleEvent()));

        delta.Should().Be(1L);
    }

    [Fact]
    public void Publish_ZeroSubscribers_DoesNotIncrementReceivedCounter()
    {
        var sut = CreateSut();

        var delta = MeasureCounterDelta(
            "meepleai.wikidata.sse.messages.received.total",
            () => sut.Publish(SampleEvent()));

        delta.Should().Be(0L,
            "with zero subscribers nothing is delivered locally, so the received counter stays put");
    }

    [Fact]
    public async Task Publish_WithThreeSubscribers_IncrementsReceivedCounterByThree()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Spin up three subscribers and wait for them to register.
        var subscriberTasks = Enumerable.Range(0, 3).Select(_ => Task.Run(async () =>
        {
            await foreach (var _ in sut.SubscribeAsync(cts.Token))
            {
                // Drain — we only care that the subscriber count is 3 when
                // Publish runs.
            }
        })).ToList();

        // Wait until all three subscribers are registered in the broadcaster.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (sut.SubscriberCount < 3 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        sut.SubscriberCount.Should().Be(3, "test prerequisite: all subscribers must be registered");

        var receivedDelta = MeasureCounterDelta(
            "meepleai.wikidata.sse.messages.received.total",
            () => sut.Publish(SampleEvent()));

        receivedDelta.Should().Be(3L,
            "the received counter MUST tick by the snapshot subscriber count at publish time");

        cts.Cancel();
        try
        {
            await Task.WhenAll(subscriberTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }
    }

    /// <summary>
    /// Captures the delta of a global counter while <paramref name="action"/>
    /// runs. The shared global <c>Meter</c> may receive concurrent ticks from
    /// other tests in the same process, so we filter measurements down to the
    /// instrument we asked for. The listener subscribes BEFORE action and is
    /// disposed AFTER, so any measurement emitted during the action window
    /// shows up in <c>captured</c>.
    /// </summary>
    private static long MeasureCounterDelta(string instrumentName, Action action)
    {
        var captured = new List<long>();

        using var listener = new System.Diagnostics.Metrics.MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == instrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => captured.Add(value));
        listener.Start();

        action();

        return captured.Sum();
    }
}
