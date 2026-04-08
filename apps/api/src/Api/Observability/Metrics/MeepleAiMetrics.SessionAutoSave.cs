// F3: AutoSave health observable gauge
using System.Diagnostics.Metrics;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Observable gauge: seconds since the last AutoSaveSessionJob execution
    /// (across all active session schedules). Reads from the singleton
    /// <see cref="IAutoSaveHealthTracker"/> resolved at registration time.
    ///
    /// Reports -1 when no run has happened yet (process startup or no active sessions).
    /// Alert when value &gt; 120 (more than 2 missed intervals at 60s each).
    /// </summary>
    /// <param name="tracker">The singleton tracker observed by the gauge.</param>
    public static void RegisterAutoSaveHealthGauge(IAutoSaveHealthTracker tracker)
    {
        Meter.CreateObservableGauge(
            name: "meepleai.session.autosave.last_run_age_seconds",
            observeValue: () => tracker.GetLastRunAgeSeconds() ?? -1,
            unit: "s",
            description: "Seconds elapsed since the last AutoSaveSessionJob execution. -1 when no run has occurred yet.");
    }
}
