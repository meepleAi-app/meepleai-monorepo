// SP5 Admin Security S2 T7 — impersonation active-sessions gauge
using System.Diagnostics.Metrics;
using Api.BoundedContexts.Administration.Infrastructure.Health;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    private static bool _impersonationGaugeRegistered;

    /// <summary>
    /// Registers an ObservableGauge reporting the latest active-impersonation count from the
    /// singleton <see cref="IImpersonationHealthTracker"/>. Idempotent — subsequent calls are
    /// ignored. Mirrors S1 T4b <c>RegisterAuditOutboxGauges</c>.
    ///
    /// Gauge: <c>meepleai.security.impersonation.active.count</c> — number of active impersonation
    /// sessions (ImpersonatedByUserId set, RevokedAt null, ImpersonatedUntil &gt; now), refreshed
    /// every ~30s by <c>ImpersonationMetricsRefreshService</c>.
    ///
    /// Suggested alerting: a sustained nonzero value during off-hours, or a value above an
    /// org-specific concurrency threshold, warrants superadmin review via the kill-switch dashboard.
    /// </summary>
    public static void RegisterImpersonationGauges(IImpersonationHealthTracker tracker)
    {
        if (_impersonationGaugeRegistered)
        {
            return;
        }
        _impersonationGaugeRegistered = true;

        Meter.CreateObservableGauge(
            name: "meepleai.security.impersonation.active.count",
            observeValue: () => tracker.GetActiveCount(),
            unit: "sessions",
            description: "Number of active impersonation sessions (refreshed every ~30s).");
    }

    /// <summary>
    /// Test-only reset hook so tests exercising <see cref="RegisterImpersonationGauges"/> can be
    /// idempotent across instantiations. NOT for production use.
    /// </summary>
    internal static void ResetImpersonationGaugeForTest()
    {
        _impersonationGaugeRegistered = false;
    }
}
