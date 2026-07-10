using System.Diagnostics.Metrics;

namespace Api.Observability;

/// <summary>#526 ME-M1.4 admin-review observability (AC-7): bulk-action counter.</summary>
internal static partial class MeepleAiMetrics
{
    /// <summary>Admin mechanic-review bulk actions, tagged {action=bulk_approve|bulk_reject}.</summary>
    public static readonly Counter<long> MechanicReviewBulkActions =
        Meter.CreateCounter<long>(
            "mechanic_review_bulk_actions_total",
            description: "Admin mechanic-review bulk actions by action.");
}
