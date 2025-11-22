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
    /// Counter for PDF upload attempts by status (success/validation_failed/storage_failed)
    /// </summary>
    public static readonly Counter<long> PdfUploadAttempts = Meter.CreateCounter<long>(
        name: "meepleai.pdf.upload.attempts",
        unit: "attempts",
        description: "PDF upload attempts by status");

    /// <summary>
    /// Histogram for PDF file size distribution in bytes
    /// </summary>
    public static readonly Histogram<long> PdfFileSizeBytes = Meter.CreateHistogram<long>(
        name: "meepleai.pdf.file.size",
        unit: "bytes",
        description: "PDF file size distribution");

    /// <summary>
    /// Histogram for PDF processing duration in milliseconds (full pipeline)
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

    /// <summary>
    /// Counter for 3-stage extraction attempts (unstructured/smoldocling/docnet)
    /// </summary>
    public static readonly Counter<long> PdfExtractionStageAttempts = Meter.CreateCounter<long>(
        name: "meepleai.pdf.extraction.stage.attempts",
        unit: "attempts",
        description: "Extraction attempts by stage");

    /// <summary>
    /// Counter for successful extractions by stage
    /// </summary>
    public static readonly Counter<long> PdfExtractionStageSuccess = Meter.CreateCounter<long>(
        name: "meepleai.pdf.extraction.stage.success",
        unit: "successes",
        description: "Successful extractions by stage");

    /// <summary>
    /// Histogram for extraction duration by stage in milliseconds
    /// </summary>
    public static readonly Histogram<double> PdfExtractionStageDuration = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.extraction.stage.duration",
        unit: "ms",
        description: "Extraction duration by stage");

    /// <summary>
    /// Histogram for PDF extraction quality scores (0.0-1.0)
    /// </summary>
    public static readonly Histogram<double> PdfQualityScore = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.quality.score",
        unit: "score",
        description: "PDF extraction quality scores");

    /// <summary>
    /// Histogram for PDF chunking duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> PdfChunkingDuration = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.chunking.duration",
        unit: "ms",
        description: "Text chunking duration");

    /// <summary>
    /// Histogram for number of chunks generated per document
    /// </summary>
    public static readonly Histogram<int> PdfChunkCount = Meter.CreateHistogram<int>(
        name: "meepleai.pdf.chunks.count",
        unit: "chunks",
        description: "Number of chunks generated per document");

    /// <summary>
    /// Histogram for embedding generation duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> PdfEmbeddingDuration = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.embedding.duration",
        unit: "ms",
        description: "Embedding generation duration");

    /// <summary>
    /// Histogram for Qdrant indexing duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> PdfIndexingDuration = Meter.CreateHistogram<double>(
        name: "meepleai.pdf.indexing.duration",
        unit: "ms",
        description: "Qdrant indexing duration");

    #endregion

    #region Streaming Metrics

    /// <summary>
    /// Histogram for streaming operation total duration in milliseconds
    /// </summary>
    public static readonly Histogram<double> StreamingTotalDuration = Meter.CreateHistogram<double>(
        name: "meepleai.streaming.total.duration",
        unit: "ms",
        description: "Total streaming operation duration");

    /// <summary>
    /// Histogram for token generation rate during streaming (tokens/second)
    /// </summary>
    public static readonly Histogram<double> StreamingTokenRate = Meter.CreateHistogram<double>(
        name: "meepleai.streaming.token.rate",
        unit: "tokens/sec",
        description: "Token generation rate during streaming");

    /// <summary>
    /// Counter for streaming requests by type (qa/explain/setup)
    /// </summary>
    public static readonly Counter<long> StreamingRequestsTotal = Meter.CreateCounter<long>(
        name: "meepleai.streaming.requests.total",
        unit: "requests",
        description: "Total streaming requests by type");

    #endregion

    #region Agent Metrics

    /// <summary>
    /// Counter for agent invocations by type (chess/feedback/followup/qa/explain/setup)
    /// </summary>
    public static readonly Counter<long> AgentInvocations = Meter.CreateCounter<long>(
        name: "meepleai.agent.invocations.total",
        unit: "invocations",
        description: "Agent invocations by type");

    /// <summary>
    /// Histogram for agent execution duration by type in milliseconds
    /// </summary>
    public static readonly Histogram<double> AgentDuration = Meter.CreateHistogram<double>(
        name: "meepleai.agent.duration",
        unit: "ms",
        description: "Agent execution duration by type");

    /// <summary>
    /// Counter for agent errors by type
    /// </summary>
    public static readonly Counter<long> AgentErrors = Meter.CreateCounter<long>(
        name: "meepleai.agent.errors.total",
        unit: "errors",
        description: "Agent errors by type");

    #endregion

    #region Hybrid Search Metrics

    /// <summary>
    /// Histogram for vector search component scores in hybrid search
    /// </summary>
    public static readonly Histogram<double> HybridSearchVectorScore = Meter.CreateHistogram<double>(
        name: "meepleai.search.vector.score",
        unit: "score",
        description: "Vector search component scores");

    /// <summary>
    /// Histogram for keyword search component scores in hybrid search
    /// </summary>
    public static readonly Histogram<double> HybridSearchKeywordScore = Meter.CreateHistogram<double>(
        name: "meepleai.search.keyword.score",
        unit: "score",
        description: "Keyword search component scores");

    /// <summary>
    /// Histogram for RRF fusion scores in hybrid search
    /// </summary>
    public static readonly Histogram<double> HybridSearchRrfScore = Meter.CreateHistogram<double>(
        name: "meepleai.search.rrf.score",
        unit: "score",
        description: "RRF fusion scores");

    /// <summary>
    /// Counter for hybrid search requests
    /// </summary>
    public static readonly Counter<long> HybridSearchTotal = Meter.CreateCounter<long>(
        name: "meepleai.search.hybrid.total",
        unit: "searches",
        description: "Total hybrid search requests");

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

    #region Error Metrics (OPS-05)

    /// <summary>
    /// Counter for API errors with detailed categorization.
    /// Tracks all API errors by endpoint, status code, exception type, and severity.
    /// Complements OpenTelemetry's http_server_request_duration_count with exception-level detail.
    /// </summary>
    public static readonly Counter<long> ApiErrorsTotal = Meter.CreateCounter<long>(
        name: "meepleai.api.errors.total",
        unit: "errors",
        description: "Total number of API errors by endpoint, status code, exception type, and severity");

    /// <summary>
    /// Counter for unhandled exceptions that bubble to exception middleware.
    /// These are the most critical errors - unexpected failures not caught by endpoint handlers.
    /// High values indicate code quality issues or missing error handling.
    /// </summary>
    public static readonly Counter<long> UnhandledErrorsTotal = Meter.CreateCounter<long>(
        name: "meepleai.api.errors.unhandled",
        unit: "errors",
        description: "Total number of unhandled exceptions caught by exception middleware");

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

    /// <summary>
    /// Records an API error with detailed categorization for OPS-05 error monitoring.
    /// Captures endpoint, HTTP status code, exception type, severity, and error category.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="httpStatusCode">HTTP status code to be returned</param>
    /// <param name="endpoint">API endpoint path (route template, not actual path with IDs)</param>
    /// <param name="isUnhandled">True if this is an unhandled exception caught by middleware</param>
    public static void RecordApiError(
        Exception exception,
        int httpStatusCode,
        string? endpoint = null,
        bool isUnhandled = false)
    {
        // Determine severity based on HTTP status code
        var severity = httpStatusCode switch
        {
            >= 500 => "critical", // Server errors
            >= 400 => "warning",  // Client errors
            _ => "info"           // Unexpected, but defensive
        };

        // Categorize error by exception type for better alerting
        var errorCategory = ClassifyException(exception, httpStatusCode);

        // Build tags for the error counter
        var tags = new TagList
        {
            { "http.status_code", httpStatusCode },
            { "exception.type", exception.GetType().Name },
            { "severity", severity },
            { "error.category", errorCategory }
        };

        // Include endpoint if available (use route template to avoid high cardinality)
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            tags.Add("http.route", endpoint);
        }

        // Record general API error counter
        ApiErrorsTotal.Add(1, tags);

        // Record unhandled error counter if applicable
        if (isUnhandled)
        {
            UnhandledErrorsTotal.Add(1, tags);
        }
    }

    /// <summary>
    /// Classifies an exception into a category for error monitoring.
    /// Categories: validation, system, dependency, timeout, authorization, notfound.
    /// </summary>
    private static string ClassifyException(Exception exception, int httpStatusCode)
    {
        // Classification based on exception type
        return exception switch
        {
            ArgumentException or ArgumentNullException => "validation",
            UnauthorizedAccessException => "authorization",
            InvalidOperationException when exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) => "notfound",
            InvalidOperationException => "system",
            TimeoutException => "timeout",
            HttpRequestException or TaskCanceledException => "dependency",
            _ when httpStatusCode == 404 => "notfound",
            _ when httpStatusCode >= 500 => "system",
            _ when httpStatusCode >= 400 => "validation",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Records PDF upload attempt with status
    /// </summary>
    public static void RecordPdfUploadAttempt(string status, long? fileSizeBytes = null)
    {
        var tags = new TagList { { "status", status } };
        PdfUploadAttempts.Add(1, tags);

        if (fileSizeBytes.HasValue)
        {
            PdfFileSizeBytes.Record(fileSizeBytes.Value, tags);
        }
    }

    /// <summary>
    /// Records PDF extraction stage attempt and duration
    /// </summary>
    public static void RecordPdfExtractionStage(
        string stageName,
        bool success,
        double durationMs,
        double? qualityScore = null)
    {
        var tags = new TagList
        {
            { "stage", stageName.ToLowerInvariant() },
            { "success", success }
        };

        PdfExtractionStageAttempts.Add(1, tags);
        PdfExtractionStageDuration.Record(durationMs, tags);

        if (success)
        {
            PdfExtractionStageSuccess.Add(1, tags);

            if (qualityScore.HasValue)
            {
                var qualityTags = new TagList { { "stage", stageName.ToLowerInvariant() } };
                PdfQualityScore.Record(qualityScore.Value, qualityTags);
            }
        }
    }

    /// <summary>
    /// Records PDF processing pipeline step duration
    /// </summary>
    public static void RecordPdfPipelineStep(
        string step,
        double durationMs,
        int? count = null)
    {
        var tags = new TagList { { "step", step.ToLowerInvariant() } };

        switch (step.ToLowerInvariant())
        {
            case "chunking":
                PdfChunkingDuration.Record(durationMs, tags);
                if (count.HasValue)
                {
                    PdfChunkCount.Record(count.Value, tags);
                }
                break;

            case "embedding":
                PdfEmbeddingDuration.Record(durationMs, tags);
                break;

            case "indexing":
                PdfIndexingDuration.Record(durationMs, tags);
                break;

            case "extraction":
                // Use PdfProcessingDuration for overall extraction
                PdfProcessingDuration.Record(durationMs, tags);
                break;
        }
    }

    /// <summary>
    /// Records streaming request with duration and token rate
    /// </summary>
    public static void RecordStreamingRequest(
        string type,
        double durationMs,
        int? totalTokens = null)
    {
        var tags = new TagList { { "type", type.ToLowerInvariant() } };

        StreamingRequestsTotal.Add(1, tags);
        StreamingTotalDuration.Record(durationMs, tags);

        if (totalTokens.HasValue && durationMs > 0)
        {
            var tokensPerSecond = totalTokens.Value / (durationMs / 1000.0);
            StreamingTokenRate.Record(tokensPerSecond, tags);
        }
    }

    /// <summary>
    /// Records agent invocation with duration
    /// </summary>
    public static void RecordAgentInvocation(
        string agentType,
        double durationMs,
        bool success = true)
    {
        var tags = new TagList
        {
            { "agent", agentType.ToLowerInvariant() },
            { "success", success }
        };

        AgentInvocations.Add(1, tags);
        AgentDuration.Record(durationMs, tags);

        if (!success)
        {
            AgentErrors.Add(1, tags);
        }
    }

    /// <summary>
    /// Records hybrid search with component scores
    /// </summary>
    public static void RecordHybridSearch(
        double? vectorScore = null,
        double? keywordScore = null,
        double? rrfScore = null)
    {
        HybridSearchTotal.Add(1);

        if (vectorScore.HasValue)
        {
            HybridSearchVectorScore.Record(vectorScore.Value);
        }

        if (keywordScore.HasValue)
        {
            HybridSearchKeywordScore.Record(keywordScore.Value);
        }

        if (rrfScore.HasValue)
        {
            HybridSearchRrfScore.Record(rrfScore.Value);
        }
    }

    #endregion
}
