using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// Verifies the AutoSave health observable gauge is registered with the exact
/// instrument name and unit that the Prometheus alert rule
/// `SessionAutoSaveJobStale` (infra/prometheus-rules.yml) depends on.
///
/// The alert expression uses the Prometheus-flattened metric name:
///   meepleai_session_autosave_last_run_age_seconds
///
/// which the OpenTelemetry Prometheus exporter derives from the OTel name
/// `meepleai.session.autosave.last_run_age_seconds` by converting dots to
/// underscores. Because the OTel name already ends in `_seconds`, the
/// exporter MUST NOT append a second `_seconds` suffix from the unit metadata.
///
/// These tests guard against future OTel exporter version bumps that could
/// silently change the naming convention and break the alert.
///
/// PR #327 — follow-up minor #3 from the code review.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Area", "Observability")]
[Trait("BoundedContext", "SessionTracking")]
public class AutoSaveHealthGaugeTests
{
    private const string ExpectedOtelInstrumentName = "meepleai.session.autosave.last_run_age_seconds";
    private const string ExpectedUnit = "s";

    /// <summary>
    /// The exact Prometheus scrape name the alert rule depends on.
    /// Keep this in sync with the expression in infra/prometheus-rules.yml
    /// (group meepleai_warning_alerts, alert SessionAutoSaveJobStale).
    /// </summary>
    private const string ExpectedPrometheusScrapeName = "meepleai_session_autosave_last_run_age_seconds";

    [Fact]
    public void RegisterAutoSaveHealthGauge_PublishesInstrumentWithExpectedNameAndUnit()
    {
        var tracker = new AutoSaveHealthTracker(new FakeTimeProvider(DateTimeOffset.UtcNow));
        Instrument? captured = null;

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                    && instrument.Name == ExpectedOtelInstrumentName)
                {
                    captured = instrument;
                }
            }
        };
        listener.Start();

        MeepleAiMetrics.RegisterAutoSaveHealthGauge(tracker);

        captured.Should().NotBeNull(
            "RegisterAutoSaveHealthGauge must publish an instrument named {0} " +
            "so the Prometheus exporter exposes it as {1} " +
            "(alert SessionAutoSaveJobStale depends on this exact name)",
            ExpectedOtelInstrumentName,
            ExpectedPrometheusScrapeName);
        captured!.Unit.Should().Be(ExpectedUnit,
            "unit must be 's' so the exporter does NOT append a second _seconds suffix");
        captured.Meter.Name.Should().Be(MeepleAiMetrics.MeterName);
    }

    [Fact]
    public void PrometheusScrapeName_DerivedFromOtelName_MatchesAlertExpression()
    {
        // Pure string assertion: the Prometheus exporter converts dots to
        // underscores. This test documents the conversion contract in code so
        // any future rename of the OTel instrument is forced to update both
        // sides (instrument + alert rule) simultaneously.
        var derived = ExpectedOtelInstrumentName.Replace('.', '_');

        derived.Should().Be(ExpectedPrometheusScrapeName,
            "if this assertion fails, update infra/prometheus-rules.yml " +
            "alert SessionAutoSaveJobStale to reference the new scrape name");
    }

    [Fact]
    public void RegisteredGauge_ObservesMinusOneSentinel_WhenTrackerHasNeverRun()
    {
        var tracker = new AutoSaveHealthTracker(new FakeTimeProvider(DateTimeOffset.UtcNow));
        var observed = new List<long>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                    && instrument.Name == ExpectedOtelInstrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => observed.Add(value));
        listener.Start();

        MeepleAiMetrics.RegisterAutoSaveHealthGauge(tracker);
        listener.RecordObservableInstruments();

        observed.Should().Contain(-1L,
            "the gauge must emit the -1 sentinel when the tracker has never " +
            "recorded a run, so the alert expression `>= 0` guard suppresses " +
            "spurious cold-start alerts");
    }

    [Fact]
    public void RegisteredGauge_ObservesElapsedSeconds_AfterRecordRun()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new FakeTimeProvider(now);
        var tracker = new AutoSaveHealthTracker(timeProvider);
        var observed = new List<long>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                    && instrument.Name == ExpectedOtelInstrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => observed.Add(value));
        listener.Start();

        MeepleAiMetrics.RegisterAutoSaveHealthGauge(tracker);

        tracker.RecordRun();
        timeProvider.Advance(TimeSpan.FromSeconds(130));
        listener.RecordObservableInstruments();

        observed.Should().Contain(130L,
            "the gauge must emit the elapsed seconds since the last run, " +
            "not the -1 sentinel, so the alert can fire above 120");
    }
}
