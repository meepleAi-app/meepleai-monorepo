// Issue #1314 PR 2: Storage layout migration observability metrics.
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
    /// Gauge-like UpDownCounter for the currently-active layout version.
    /// Value 1 is incremented each time the drainer pod starts; the
    /// <c>layout_version</c> tag carries the human-readable label
    /// ("v1-gameId", "v1-gameId-migrating", "v2-categorized").
    /// Useful for confirming a deployed env is actually on the expected mode.
    /// </summary>
    public static readonly Counter<long> StorageLayoutVersionAnnouncementsTotal = Meter.CreateCounter<long>(
        name: "meepleai.storage.layout.version.announcements.total",
        unit: "events",
        description: "Storage-layout version announcement events on drainer startup, tagged by layout_version");
}
