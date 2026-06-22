using Api.BoundedContexts.Administration.Infrastructure.External;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Queries.Metrics;

/// <summary>
/// Handler for <see cref="GetPrometheusMetricLabelsQuery"/>.
///
/// <para>Wraps the Prometheus labels endpoint with a 60s HybridCache layer so
/// repeated calls from the MetricSelector dropdown don't hammer Prometheus.
/// When Prometheus is unreachable, returns <see cref="FallbackLabels"/> with
/// <see cref="MetricLabelsResult.IsFallback"/>=true so the UI can show a
/// "Prometheus offline · cached labels" hint.</para>
/// </summary>
internal sealed class GetPrometheusMetricLabelsQueryHandler
    : IRequestHandler<GetPrometheusMetricLabelsQuery, MetricLabelsResult>
{
    /// <summary>
    /// Cache key used by HybridCache. Single global key — all admins see the
    /// same metric list because the catalog is system-wide.
    /// </summary>
    internal const string CacheKey = "admin:alerts:metric-labels";

    /// <summary>
    /// Hard-coded fallback returned when Prometheus is offline. The five
    /// metrics here are the "common case" alerts referenced in the SP5 F4-C7
    /// mockup (sp5-admin-alerts.html); enough to let admins create at least
    /// the canonical rules during an outage.
    /// </summary>
    internal static readonly IReadOnlyList<string> FallbackLabels = new[]
    {
        "meepleai_chat_p95_ms",
        "meepleai_embedding_queue_depth",
        "meepleai_rag_cost_per_request",
        "meepleai_api_error_rate",
        "meepleai_pdf_processing_failed",
    };

    private static readonly HybridCacheEntryOptions DefaultCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(60),
        LocalCacheExpiration = TimeSpan.FromSeconds(60),
    };

    private readonly IPrometheusLabelsClient _labelsClient;
    private readonly HybridCache _cache;
    private readonly ILogger<GetPrometheusMetricLabelsQueryHandler> _logger;

    public GetPrometheusMetricLabelsQueryHandler(
        IPrometheusLabelsClient labelsClient,
        HybridCache cache,
        ILogger<GetPrometheusMetricLabelsQueryHandler> logger)
    {
        _labelsClient = labelsClient ?? throw new ArgumentNullException(nameof(labelsClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MetricLabelsResult> Handle(
        GetPrometheusMetricLabelsQuery request,
        CancellationToken cancellationToken)
    {
        // HybridCache populates via the factory delegate on miss. We store a
        // discriminator alongside the labels so the caller can know whether
        // we served real or fallback data without an extra round-trip.
        var cached = await _cache.GetOrCreateAsync(
            CacheKey,
            FetchLabelsAsync,
            DefaultCacheOptions,
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return cached;
    }

    private async ValueTask<MetricLabelsResult> FetchLabelsAsync(CancellationToken cancellationToken)
    {
        var live = await _labelsClient.GetMetricNamesAsync(cancellationToken).ConfigureAwait(false);
        if (live is null || live.Count == 0)
        {
            _logger.LogWarning(
                "Prometheus labels endpoint unavailable — returning fallback list of {Count} metric names",
                FallbackLabels.Count);
            return new MetricLabelsResult(FallbackLabels, IsFallback: true);
        }

        return new MetricLabelsResult(live, IsFallback: false);
    }
}
