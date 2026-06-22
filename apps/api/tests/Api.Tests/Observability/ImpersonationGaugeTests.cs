using System.Diagnostics.Metrics;
using Api.BoundedContexts.Administration.Infrastructure.Health;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// Unit tests for the SP5 S2 T7 impersonation active-count ObservableGauge. Verifies the gauge
/// emits the value held by the singleton <see cref="IImpersonationHealthTracker"/> via the
/// MeterListener API. Mirrors S1 T4b gauge tests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ImpersonationGaugeTests
{
    private const string GaugeName = "meepleai.security.impersonation.active.count";

    [Fact]
    public void RegisterImpersonationGauges_PublishesActiveCountFromTracker()
    {
        var tracker = new ImpersonationHealthTracker();
        tracker.SetActiveCount(4);

        MeepleAiMetrics.ResetImpersonationGaugeForTest();
        try
        {
            long? observed = null;
            using var listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                        && instrument.Name == GaugeName)
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            listener.SetMeasurementEventCallback<long>((_, value, _, _) => observed = value);
            listener.Start();

            MeepleAiMetrics.RegisterImpersonationGauges(tracker);
            listener.RecordObservableInstruments();

            observed.Should().Be(4, "the gauge reports the tracker's active-impersonation count");
        }
        finally
        {
            MeepleAiMetrics.ResetImpersonationGaugeForTest();
        }
    }

    [Fact]
    public void Gauge_ReflectsTrackerUpdates_OnSubsequentCollections()
    {
        var tracker = new ImpersonationHealthTracker();

        MeepleAiMetrics.ResetImpersonationGaugeForTest();
        try
        {
            var observations = new List<long>();
            using var listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                        && instrument.Name == GaugeName)
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            listener.SetMeasurementEventCallback<long>((_, value, _, _) => observations.Add(value));
            listener.Start();

            MeepleAiMetrics.RegisterImpersonationGauges(tracker);

            tracker.SetActiveCount(2);
            listener.RecordObservableInstruments();
            tracker.SetActiveCount(0);
            listener.RecordObservableInstruments();

            // The gauge re-reads the tracker on each collection (the refresh service drives the value).
            observations.Should().ContainInOrder(new[] { 2L, 0L });
        }
        finally
        {
            MeepleAiMetrics.ResetImpersonationGaugeForTest();
        }
    }
}
