using Api.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #2470 — binds the runtime broadcaster and heartbeat-tracker to the
/// static <see cref="MeepleAiMetrics"/> SSE gauges. The gauges live on the
/// static partial class so they can be created at type-init time; this
/// hosted service supplies their callbacks once the DI graph is available.
/// </summary>
/// <remarks>
/// <para>Binding is idempotent: a second <see cref="StartAsync(CancellationToken)"/>
/// (which can happen in tests that re-host the application) simply re-points
/// the callbacks at the latest instances. <see cref="StopAsync(CancellationToken)"/>
/// is intentionally a no-op — the gauges remain valid throughout the host
/// lifetime, and clearing the callbacks on stop would race with metric
/// scrapes during graceful shutdown.</para>
/// </remarks>
internal sealed class WikidataSseGaugeBinder : IHostedService
{
    private readonly IWikidataEnrichmentEventBroadcaster _broadcaster;
    private readonly IWikidataAdminClientHeartbeatTracker _heartbeatTracker;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WikidataSseGaugeBinder> _logger;

    public WikidataSseGaugeBinder(
        IWikidataEnrichmentEventBroadcaster broadcaster,
        IWikidataAdminClientHeartbeatTracker heartbeatTracker,
        TimeProvider timeProvider,
        ILogger<WikidataSseGaugeBinder> logger)
    {
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _heartbeatTracker = heartbeatTracker ?? throw new ArgumentNullException(nameof(heartbeatTracker));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        MeepleAiMetrics.SetWikidataSseSubscribersCallback(() => _broadcaster.SubscriberCount);
        MeepleAiMetrics.SetWikidataSseAdminClientsConnectedCallback(
            () => _heartbeatTracker.GetConnectedCount(_timeProvider.GetUtcNow().UtcDateTime));

        _logger.LogDebug("WikidataSseGaugeBinder: callbacks registered for SSE gauges");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Intentional no-op — see class remarks.
        return Task.CompletedTask;
    }
}
