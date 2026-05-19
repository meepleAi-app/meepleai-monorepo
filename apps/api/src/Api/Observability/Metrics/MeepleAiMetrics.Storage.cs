// Issue #1314 PR 2: Storage layout migration observability metrics.
using Api.Services.Pdf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

    private static IServiceProvider? _layoutVersionGaugeRoot;

    /// <summary>
    /// Wires the current-layout-version gauge to the application's DI root.
    /// Must be called once at startup (after <c>BuildServiceProvider</c>) so
    /// the gauge can resolve <see cref="StorageLayoutOptions"/> at scrape
    /// time. Idempotent — subsequent calls are ignored.
    /// </summary>
    internal static void RegisterStorageLayoutVersionGauge(IServiceProvider serviceProvider)
    {
        if (_layoutVersionGaugeRoot is not null)
        {
            return;
        }
        _layoutVersionGaugeRoot = serviceProvider;

        // ObservableGauge resolves StorageLayoutOptions at scrape time, so a
        // pod that flips StorageLayout:WriteMode without restart (e.g. via
        // IOptionsMonitor) would surface the change on the next Prometheus
        // scrape. As of this writing the application reads options once at
        // startup, so in practice the gauge value matches the announcement
        // counter — but the gauge is the correct queryable surface for
        // "what layout version is active right now in this pod".
        Meter.CreateObservableGauge<int>(
            name: "meepleai.storage.layout.version.current",
            observeValue: () =>
            {
                using var scope = _layoutVersionGaugeRoot!.CreateScope();
                var options = scope.ServiceProvider.GetRequiredService<IOptions<StorageLayoutOptions>>().Value;
                return new Measurement<int>(
                    LayoutVersionToInt(options.WriteMode),
                    new KeyValuePair<string, object?>("layout_version", options.LayoutVersionLabel));
            },
            unit: "version",
            description: "Currently-active storage layout version. 1=v1-gameId, 2=v1-gameId-migrating, 3=v2-categorized");
    }

    /// <summary>
    /// Stable integer encoding of the layout version. Prometheus does not
    /// support string-valued gauges, so we expose an int + carry the human
    /// label as a tag.
    /// </summary>
    private static int LayoutVersionToInt(StorageWriteMode mode) => mode switch
    {
        StorageWriteMode.Legacy => 1,
        StorageWriteMode.Dual => 2,
        StorageWriteMode.New => 3,
        _ => 0,
    };
}
