// OPS-02: Custom OpenTelemetry Metrics for MeepleAI
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

/// <summary>
/// Provides custom metrics for the MeepleAI application.
/// These metrics are exported via OpenTelemetry to Prometheus.
/// </summary>
public static class MeepleAiMetrics
{
    /// <summary>
    /// Meter name for MeepleAI metrics (matches the meter name in OpenTelemetry configuration)
    /// </summary>
    public const string MeterName = "MeepleAI.Api";

    /// <summary>
    /// Meter instance for creating metrics
    /// </summary>
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    #region RAG/AI Metrics

    /// <summary>
    /// Counter for total RAG requests
    /// </summary>
    public static readonly Counter<long> RagRequestsTotal = Meter.CreateCounter<long>(
        name: "meepleai.rag.requests.total",
        unit: "requests",
        description: "Total number of RAG (Retrieval-Augmented Generation) requests");

    /// <summary>
    /// Histogram for RAG request duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> RagRequestDuration = Meter.CreateHistogram<double>(
        name: "meepleai.rag.request.duration",
        unit: "ms",
        description: "RAG request duration in milliseconds");

    /// <summary>
    /// Histogram for AI tokens used per request
    /// </summary>
    public static readonly Histogram<long> TokensUsed = Meter.CreateHistogram<long>(
        name: "meepleai.ai.tokens.used",
        unit: "tokens",
        description: "AI tokens used per request");

    /// <summary>
    /// Histogram for confidence score distribution (0.0 to 1.0)
    /// </summary>
    public static readonly Histogram<double> ConfidenceScore = Meter.CreateHistogram<double>(
        name: "meepleai.rag.confidence.score",
        unit: "score",
        description: "Confidence score distribution for RAG responses");

    /// <summary>
    /// Counter for RAG errors by type
    /// </summary>
    public static readonly Counter<long> RagErrorsTotal = Meter.CreateCounter<long>(
        name: "meepleai.rag.errors.total",
        unit: "errors",
        description: "Total number of RAG errors by type");

    #endregion

    #region Vector Search Metrics

    /// <summary>
    /// Counter for total vector searches
    /// </summary>
    public static readonly Counter<long> VectorSearchTotal = Meter.CreateCounter<long>(
        name: "meepleai.vector.search.total",
        unit: "searches",
        description: "Total number of vector searches");

    /// <summary>
    /// Histogram for vector search duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> VectorSearchDuration = Meter.CreateHistogram<double>(
        name: "meepleai.vector.search.duration",
        unit: "ms",
        description: "Vector search duration in milliseconds");

    /// <summary>
    /// Histogram for number of results returned from vector search
    /// </summary>
    public static readonly Histogram<int> VectorResultsCount = Meter.CreateHistogram<int>(
        name: "meepleai.vector.results.count",
        unit: "results",
        description: "Number of results returned from vector search");

    /// <summary>
    /// Histogram for vector indexing duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> VectorIndexingDuration = Meter.CreateHistogram<double>(
        name: "meepleai.vector.indexing.duration",
        unit: "ms",
        description: "Vector indexing duration in milliseconds");

    #endregion

    #region PDF Processing Metrics

    /// <summary>
    /// Counter for total PDF uploads
    /// </summary>
    public static readonly Counter<long> PdfUploadTotal = Meter.CreateCounter<long>(
        name: "meepleai.pdf.upload.total",
        unit: "uploads",
        description: "Total number of PDF uploads");

    /// <summary>
    /// Histogram for PDF processing duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> PdfProcessingDuration = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.processing.duration",
        unit: "ms",
        description: "PDF processing duration in milliseconds");

    /// <summary>
    /// Counter for pages processed
    /// </summary>
    public static readonly Counter<long> PdfPagesProcessed = Meter.CreateCounter<long>(
        name: "meepleai.pdf.pages.processed",
        unit: "pages",
        description: "Total number of PDF pages processed");

    /// <summary>
    /// Counter for PDF extraction errors
    /// </summary>
    public static readonly Counter<long> PdfExtractionErrors = Meter.CreateCounter<long>(
        name: "meepleai.pdf.extraction.errors",
        unit: "errors",
        description: "Total number of PDF extraction errors");

    #endregion

    #region Cache Metrics

    /// <summary>
    /// Counter for cache hits
    /// </summary>
    public static readonly Counter<long> CacheHitsTotal = Meter.CreateCounter<long>(
        name: "meepleai.cache.hits.total",
        unit: "hits",
        description: "Total number of cache hits");

    /// <summary>
    /// Counter for cache misses
    /// </summary>
    public static readonly Counter<long> CacheMissesTotal = Meter.CreateCounter<long>(
        name: "meepleai.cache.misses.total",
        unit: "misses",
        description: "Total number of cache misses");

    /// <summary>
    /// Counter for cache evictions
    /// </summary>
    public static readonly Counter<long> CacheEvictionsTotal = Meter.CreateCounter<long>(
        name: "meepleai.cache.evictions.total",
        unit: "evictions",
        description: "Total number of cache evictions");

    #endregion

    #region Helper Methods

    /// <summary>
    /// Records a RAG request with duration and metadata
    /// </summary>
    public static void RecordRagRequest(double durationMs, string? gameId = null, bool success = true)
    {
        var tags = new TagList();
        if (!string.IsNullOrEmpty(gameId))
        {
            tags.Add("game.id", gameId);
        }
        tags.Add("success", success);

        RagRequestsTotal.Add(1, tags);
        RagRequestDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records a vector search operation
    /// </summary>
    public static void RecordVectorSearch(double durationMs, int resultsCount, string? collectionName = null)
    {
        var tags = new TagList();
        if (!string.IsNullOrEmpty(collectionName))
        {
            tags.Add("collection", collectionName);
        }

        VectorSearchTotal.Add(1, tags);
        VectorSearchDuration.Record(durationMs, tags);
        VectorResultsCount.Record(resultsCount, tags);
    }

    /// <summary>
    /// Records cache hit or miss
    /// </summary>
    public static void RecordCacheAccess(bool isHit, string? cacheType = null)
    {
        var tags = new TagList();
        if (!string.IsNullOrEmpty(cacheType))
        {
            tags.Add("cache.type", cacheType);
        }

        if (isHit)
        {
            CacheHitsTotal.Add(1, tags);
        }
        else
        {
            CacheMissesTotal.Add(1, tags);
        }
    }

    #endregion
}
