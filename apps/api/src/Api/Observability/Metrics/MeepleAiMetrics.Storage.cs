// Issue #1314 PR 2: Storage layout migration observability metrics.
// Issue #1399 (2026-05-21): WriteMode/ReadMode branching removed. The gauge
// now reports the steady-state "v2-categorized" layout (integer 3) with the
// matching label. The counter/histogram surfaces remain unchanged for the
// reusable outbox drainer (future SessionPhoto / GameImage migrations).
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Counter for storage-migration objects moved by the outbox drainer.
    /// Tagged by <c>status</c> (success | failed | failed_permanent) and
    /// <c>category</c> (Pdf | SessionPhoto | GameImage | VisionSnapshot |
    /// GamebookPhoto | PhotoBatch). Used by the Phase 1 dashboard to track
    /// migration progress and error rate.
    /// </summary>
    public static readonly Counter<long> StorageMigrationObjectsMigratedTotal = Meter.CreateCounter<long>(
        name: "meepleai.storage.migration.objects_migrated.total",
        unit: "objects",
        description: "Total storage-layout migration object moves, tagged by status + category");

    /// <summary>
    /// Histogram for per-object move duration in milliseconds (CopyObjectAsync
    /// + DeleteObjectAsync round-trip). Tagged by <c>category</c>. Drives the
    /// Phase 1 SLO ("p99 &lt; 5s per object").
    /// </summary>
    public static readonly Histogram<double> StorageMigrationDurationMs = Meter.CreateHistogram<double>(
        name: "meepleai.storage.migration.duration",
        unit: "ms",
        description: "Storage-layout migration per-object move duration in milliseconds");

    /// <summary>
    /// Per-drainer-lifecycle counter that increments once at startup, tagged
    /// with the active <c>layout_version</c> label. This is an EVENT counter
    /// — useful for tracking when a pod restart switched layout versions —
    /// NOT a current-state indicator. For current state, query the
    /// <c>meepleai.storage.layout.version.current</c> ObservableGauge
    /// registered via <see cref="RegisterStorageLayoutVersionGauge"/>.
    /// </summary>
    public static readonly Counter<long> StorageLayoutVersionAnnouncementsTotal = Meter.CreateCounter<long>(
        name: "meepleai.storage.layout.version.announcements.total",
        unit: "events",
        description: "Storage-layout version announcement events on drainer startup, tagged by layout_version");

    /// <summary>
    /// Human-readable layout version label exposed on the gauge + announcement
    /// counter. Issue #1399: hardcoded to <c>v2-categorized</c> post-Phase 4
    /// — the dynamic WriteMode/ReadMode toggles are gone. Kept as a const so
    /// Prometheus dashboards and the drainer announcement remain symmetric.
    /// </summary>
    internal const string CurrentLayoutVersionLabel = "v2-categorized";

    private static bool _layoutVersionGaugeRegistered;

    /// <summary>
    /// Wires the current-layout-version gauge. Idempotent — subsequent calls
    /// are ignored. Issue #1399: the gauge now reports a constant value (3)
    /// since the dynamic write/read modes were removed. Kept for dashboard
    /// backward-compat so existing PromQL queries do not 404.
    /// </summary>
    internal static void RegisterStorageLayoutVersionGauge(IServiceProvider serviceProvider)
    {
        _ = serviceProvider; // Parameter retained for API stability; no longer used.
        if (_layoutVersionGaugeRegistered)
        {
            return;
        }
        _layoutVersionGaugeRegistered = true;

        Meter.CreateObservableGauge<int>(
            name: "meepleai.storage.layout.version.current",
            observeValue: () => new Measurement<int>(
                3,
                new KeyValuePair<string, object?>("layout_version", CurrentLayoutVersionLabel)),
            unit: "version",
            description: "Currently-active storage layout version. Post-#1399 the value is permanently 3 (v2-categorized).");
    }
}
