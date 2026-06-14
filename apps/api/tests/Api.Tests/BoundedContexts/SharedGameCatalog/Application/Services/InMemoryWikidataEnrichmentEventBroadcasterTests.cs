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
}
