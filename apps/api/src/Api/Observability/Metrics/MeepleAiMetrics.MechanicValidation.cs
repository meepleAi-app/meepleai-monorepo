// ADR-051 Sprint 1 / Task 33: Mechanic-validation pipeline metrics.
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    #region MechanicValidation Metrics (ADR-051 Sprint 1 / Task 33)

    /// <summary>
    /// Counter for total mechanic-analysis metric computations.
    /// ADR-051 Sprint 1 / Task 33: Tracks invocations of the metrics-computation pipeline.
    /// Tagged with <c>certification_status</c> (certified|not_certified|not_evaluated) for
    /// slice-by-status dashboards.
    /// </summary>
    public static readonly Counter<long> MetricsComputed = Meter.CreateCounter<long>(
        name: "meepleai.mechanic_validation.metrics_computed",
        unit: "events",
        description: "Total mechanic-analysis metric computations.");

    /// <summary>
    /// Counter for mechanic-analyses that became Certified via the automatic threshold path.
    /// ADR-051 Sprint 1 / Task 33: Distinguishes automatic certification from admin overrides
    /// (see <see cref="Overrides"/>).
    /// </summary>
    public static readonly Counter<long> CertificationsGranted = Meter.CreateCounter<long>(
        name: "meepleai.mechanic_validation.certifications_granted",
        unit: "events",
        description: "Mechanic-analyses that became Certified via the automatic threshold path.");

    /// <summary>
    /// Counter for admin-override certifications applied to mechanic-analyses.
    /// ADR-051 Sprint 1 / Task 33: Counterpart to <see cref="CertificationsGranted"/> for the
    /// override-driven certification path.
    /// </summary>
    public static readonly Counter<long> Overrides = Meter.CreateCounter<long>(
        name: "meepleai.mechanic_validation.overrides",
        unit: "events",
        description: "Admin-override certifications applied to mechanic-analyses.");

    /// <summary>
    /// Histogram for mechanic-matching engine wall-clock duration in milliseconds.
    /// ADR-051 Sprint 1 / Task 33: Measures the synchronous matching call only; embedding and
    /// keyword-extraction overhead are excluded.
    /// </summary>
    public static readonly Histogram<double> MatchingDuration = Meter.CreateHistogram<double>(
        name: "meepleai.mechanic_validation.matching_duration",
        unit: "ms",
        description: "Wall-clock duration of the matching engine in milliseconds.");

    /// <summary>
    /// Histogram for the composite overall score (0..1) recorded per metrics computation.
    /// ADR-051 Sprint 1 / Task 33: Stateless alternative to a per-shared-game gauge — Prometheus
    /// quantile aggregation over this histogram yields percentile dashboards.
    /// </summary>
    public static readonly Histogram<double> OverallScore = Meter.CreateHistogram<double>(
        name: "meepleai.mechanic_validation.overall_score",
        unit: "score",
        description: "Overall score (0..1 composite of coverage/page/bgg) recorded per metrics computation.");

    #endregion
}
