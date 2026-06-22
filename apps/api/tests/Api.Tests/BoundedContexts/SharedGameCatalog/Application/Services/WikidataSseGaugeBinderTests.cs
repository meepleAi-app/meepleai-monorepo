using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Observability;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #2470 — TDD tests for <see cref="WikidataSseGaugeBinder"/>.
/// </summary>
[Collection("WikidataMetrics")]
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2470")]
public class WikidataSseGaugeBinderTests
{
    [Fact]
    public async Task StartAsync_BindsSubscribersCallback_ToBroadcasterCount()
    {
        var broadcaster = new FakeBroadcaster { SubscriberCount = 4 };
        var tracker = new FakeHeartbeatTracker { ConnectedCount = 0 };
        var binder = CreateBinder(broadcaster, tracker, FakeTimeProvider.AtFixed(new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc)));

        try
        {
            await binder.StartAsync(CancellationToken.None);

            ReadObservableGauge(MeepleAiMetrics.WikidataSseSubscribers).Should().Be(4);
        }
        finally
        {
            MeepleAiMetrics.ResetWikidataSseSubscribersCallback();
            MeepleAiMetrics.ResetWikidataSseAdminClientsConnectedCallback();
        }
    }

    [Fact]
    public async Task StartAsync_BindsAdminClientsCallback_ToTrackerCount()
    {
        var broadcaster = new FakeBroadcaster { SubscriberCount = 0 };
        var tracker = new FakeHeartbeatTracker { ConnectedCount = 3 };
        var binder = CreateBinder(broadcaster, tracker, FakeTimeProvider.AtFixed(new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc)));

        try
        {
            await binder.StartAsync(CancellationToken.None);

            ReadObservableGauge(MeepleAiMetrics.WikidataSseAdminClientsConnected).Should().Be(3);
        }
        finally
        {
            MeepleAiMetrics.ResetWikidataSseSubscribersCallback();
            MeepleAiMetrics.ResetWikidataSseAdminClientsConnectedCallback();
        }
    }

    [Fact]
    public async Task StartAsync_TrackerCallback_UsesCurrentUtcFromTimeProvider()
    {
        var broadcaster = new FakeBroadcaster { SubscriberCount = 0 };
        var tracker = new FakeHeartbeatTracker { ConnectedCount = 7 };
        var capturedUtc = new List<DateTime>();
        tracker.OnGetConnectedCount = utc => capturedUtc.Add(utc);

        var fixedUtc = new DateTime(2026, 6, 22, 13, 30, 0, DateTimeKind.Utc);
        var binder = CreateBinder(broadcaster, tracker, FakeTimeProvider.AtFixed(fixedUtc));

        try
        {
            await binder.StartAsync(CancellationToken.None);

            ReadObservableGauge(MeepleAiMetrics.WikidataSseAdminClientsConnected);

            capturedUtc.Should().NotBeEmpty();
            capturedUtc[^1].Should().Be(fixedUtc, "the tracker MUST receive the UTC from TimeProvider, not DateTime.UtcNow");
        }
        finally
        {
            MeepleAiMetrics.ResetWikidataSseSubscribersCallback();
            MeepleAiMetrics.ResetWikidataSseAdminClientsConnectedCallback();
        }
    }

    [Fact]
    public async Task StartAsync_CalledTwice_LatestBroadcasterWins()
    {
        var firstBroadcaster = new FakeBroadcaster { SubscriberCount = 1 };
        var secondBroadcaster = new FakeBroadcaster { SubscriberCount = 9 };
        var tracker = new FakeHeartbeatTracker { ConnectedCount = 0 };
        var time = FakeTimeProvider.AtFixed(new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc));

        try
        {
            var first = CreateBinder(firstBroadcaster, tracker, time);
            await first.StartAsync(CancellationToken.None);

            var second = CreateBinder(secondBroadcaster, tracker, time);
            await second.StartAsync(CancellationToken.None);

            ReadObservableGauge(MeepleAiMetrics.WikidataSseSubscribers)
                .Should().Be(9, "the latest StartAsync MUST swap the callback atomically");
        }
        finally
        {
            MeepleAiMetrics.ResetWikidataSseSubscribersCallback();
            MeepleAiMetrics.ResetWikidataSseAdminClientsConnectedCallback();
        }
    }

    [Fact]
    public async Task StopAsync_IsNoOp_GaugesRemainValid()
    {
        var broadcaster = new FakeBroadcaster { SubscriberCount = 2 };
        var tracker = new FakeHeartbeatTracker { ConnectedCount = 1 };
        var binder = CreateBinder(broadcaster, tracker, FakeTimeProvider.AtFixed(new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc)));

        try
        {
            await binder.StartAsync(CancellationToken.None);
            await binder.StopAsync(CancellationToken.None);

            ReadObservableGauge(MeepleAiMetrics.WikidataSseSubscribers).Should().Be(2);
            ReadObservableGauge(MeepleAiMetrics.WikidataSseAdminClientsConnected).Should().Be(1);
        }
        finally
        {
            MeepleAiMetrics.ResetWikidataSseSubscribersCallback();
            MeepleAiMetrics.ResetWikidataSseAdminClientsConnectedCallback();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Constructor_NullDependency_Throws(int nullDependencyIndex)
    {
        var broadcaster = nullDependencyIndex == 0 ? null! : new FakeBroadcaster();
        var tracker = nullDependencyIndex == 1 ? null! : new FakeHeartbeatTracker();
        var time = nullDependencyIndex == 2 ? null! : FakeTimeProvider.AtFixed(DateTime.UtcNow);
        var logger = nullDependencyIndex == 3
            ? null!
            : NullLogger<WikidataSseGaugeBinder>.Instance;

        var act = () => new WikidataSseGaugeBinder(broadcaster, tracker, time, logger);
        act.Should().Throw<ArgumentNullException>();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private static WikidataSseGaugeBinder CreateBinder(
        IWikidataEnrichmentEventBroadcaster broadcaster,
        IWikidataAdminClientHeartbeatTracker tracker,
        TimeProvider time)
        => new(broadcaster, tracker, time, NullLogger<WikidataSseGaugeBinder>.Instance);

    private static int ReadObservableGauge(System.Diagnostics.Metrics.ObservableGauge<int> gauge)
    {
        var collected = new List<int>();

        using var listener = new System.Diagnostics.Metrics.MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument == gauge)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<int>((_, value, _, _) => collected.Add(value));
        listener.Start();
        listener.RecordObservableInstruments();

        collected.Should().HaveCountGreaterThanOrEqualTo(1);
        return collected[^1];
    }

    private sealed class FakeBroadcaster : IWikidataEnrichmentEventBroadcaster
    {
        public int SubscriberCount { get; set; }
        public void Publish(WikidataEnrichmentEvent payload) { }
        public IAsyncEnumerable<WikidataEnrichmentEvent> SubscribeAsync(CancellationToken cancellationToken)
            => AsyncEnumerable.Empty<WikidataEnrichmentEvent>();
    }

    private sealed class FakeHeartbeatTracker : IWikidataAdminClientHeartbeatTracker
    {
        public int ConnectedCount { get; set; }
        public Action<DateTime>? OnGetConnectedCount { get; set; }
        public void RecordHeartbeat(Guid userId, DateTime utcNow) { }
        public int GetConnectedCount(DateTime utcNow)
        {
            OnGetConnectedCount?.Invoke(utcNow);
            return ConnectedCount;
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        private FakeTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
        public static FakeTimeProvider AtFixed(DateTime utc) => new(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));
    }

    private static class AsyncEnumerable
    {
        public static async IAsyncEnumerable<T> Empty<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
