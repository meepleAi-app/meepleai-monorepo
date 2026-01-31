// OPS-02: Custom OpenTelemetry Metrics for MeepleAI
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

/// <summary>
/// Provides custom metrics for the MeepleAI application.
/// These metrics are exported via OpenTelemetry to Prometheus.
/// </summary>
internal static class MeepleAiMetrics
{
    /// <summary>
    /// Meter name for MeepleAI metrics (matches the meter name in OpenTelemetry configuration)
    /// </summary>
    public const string MeterName = "MeepleAI.Api";

    /// <summary>
    /// Meter instance for creating metrics
    /// </summary>
    private static readonly Meter Meter = new(MeterName, "1.0.0");
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
    /// <summary>
    /// Counter for LLM token usage following OpenTelemetry GenAI semantic conventions.
    /// Tracks prompt_tokens, completion_tokens, and total_tokens by model and provider.
    /// </summary>
    /// <remarks>
    /// OpenTelemetry GenAI Semantic Conventions:
    /// - gen_ai.usage.prompt_tokens
    /// - gen_ai.usage.completion_tokens
    /// - gen_ai.usage.total_tokens
    /// Tags: model_id, provider, agent_type
    /// </remarks>
    public static readonly Counter<long> GenAiTokenUsage = Meter.CreateCounter<long>(
        name: "gen_ai.client.token.usage",
        unit: "tokens",
        description: "LLM token usage by type (prompt/completion) following OpenTelemetry GenAI semantic conventions");

    /// <summary>
    /// Histogram for LLM operation duration following OpenTelemetry GenAI semantic conventions.
    /// Tracks latency of LLM calls by model and operation type.
    /// </summary>
    /// <remarks>
    /// OpenTelemetry GenAI Semantic Convention: gen_ai.client.operation.duration
    /// </remarks>
    public static readonly Histogram<double> GenAiOperationDuration = Meter.CreateHistogram<double>(
        name: "gen_ai.client.operation.duration",
        unit: "ms",
        description: "LLM operation duration following OpenTelemetry GenAI semantic conventions");

    /// <summary>
    /// Histogram for LLM cost tracking per invocation in USD.
    /// Tracks estimated cost for each LLM call based on token usage and provider pricing.
    /// </summary>
    public static readonly Histogram<double> LlmCostUsd = Meter.CreateHistogram<double>(
        name: "meepleai.llm.cost.usd",
        unit: "usd",
        description: "LLM cost in USD per invocation by model and provider");

    /// <summary>
    /// Counter for agent token usage by agent type.
    /// Tracks total tokens consumed by each agent type for cost attribution and monitoring.
    /// </summary>
    public static readonly Counter<long> AgentTokenUsage = Meter.CreateCounter<long>(
        name: "meepleai.agent.tokens.total",
        unit: "tokens",
        description: "Total tokens used by agent type");

    /// <summary>
    /// Histogram for agent invocation cost in USD by agent type.
    /// Tracks cost per agent invocation for budget monitoring and optimization.
    /// </summary>
    public static readonly Histogram<double> AgentCostUsd = Meter.CreateHistogram<double>(
        name: "meepleai.agent.cost.usd",
        unit: "usd",
        description: "Agent invocation cost in USD by agent type");
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

    /// <summary>
    /// Counter for dashboard cache invalidations triggered by configuration changes.
    /// Issue #879: Tracks cache invalidation events for monitoring and observability.
    /// </summary>
    public static readonly Counter<long> DashboardCacheInvalidationsTotal = Meter.CreateCounter<long>(
        name: "meepleai.cache.dashboard.invalidations.total",
        unit: "invalidations",
        description: "Dashboard cache invalidations triggered by configuration changes");

    /// <summary>
    /// Counter for orphaned analysis tasks cleaned up by background job.
    /// ISSUE-2528: Tracks automatic cleanup of stale failed/cancelled tasks.
    /// </summary>
    public static readonly Counter<long> OrphanedTasksCleanedTotal = Meter.CreateCounter<long>(
        name: "meepleai.background.orphaned_tasks_cleaned.total",
        unit: "tasks",
        description: "Total number of orphaned analysis tasks cleaned up by background job");

    /// <summary>
    /// Counter for failed TOTP verification attempts.
    /// Tracks brute force attack patterns and authentication failures.
    /// </summary>
    public static readonly Counter<long> TwoFactorFailedTotpAttempts = Meter.CreateCounter<long>(
        name: "meepleai.2fa.failed_totp_attempts.total",
        unit: "attempts",
        description: "Total number of failed TOTP verification attempts");

    /// <summary>
    /// Counter for failed backup code verification attempts.
    /// Tracks potential backup code brute force attacks.
    /// </summary>
    public static readonly Counter<long> TwoFactorFailedBackupAttempts = Meter.CreateCounter<long>(
        name: "meepleai.2fa.failed_backup_attempts.total",
        unit: "attempts",
        description: "Total number of failed backup code verification attempts");

    /// <summary>
    /// Counter for blocked TOTP replay attacks.
    /// Tracks security threats from code reuse attempts.
    /// </summary>
    public static readonly Counter<long> TwoFactorReplayAttacksBlocked = Meter.CreateCounter<long>(
        name: "meepleai.2fa.replay_attacks_blocked.total",
        unit: "attacks",
        description: "Total number of TOTP replay attacks blocked");

    /// <summary>
    /// Counter for successful TOTP verifications.
    /// Baseline metric for calculating failure rates and success ratios.
    /// </summary>
    public static readonly Counter<long> TwoFactorSuccessfulTotpVerifications = Meter.CreateCounter<long>(
        name: "meepleai.2fa.successful_totp.total",
        unit: "successes",
        description: "Total number of successful TOTP verifications");

    /// <summary>
    /// Counter for successful backup code uses.
    /// Tracks backup code consumption for monitoring remaining codes per user.
    /// </summary>
    public static readonly Counter<long> TwoFactorSuccessfulBackupCodeUses = Meter.CreateCounter<long>(
        name: "meepleai.2fa.successful_backup.total",
        unit: "successes",
        description: "Total number of successful backup code uses");

    /// <summary>
    /// Counter for 2FA setup operations (generate TOTP secret + backup codes).
    /// Tracks user adoption and onboarding metrics.
    /// </summary>
    public static readonly Counter<long> TwoFactorSetupTotal = Meter.CreateCounter<long>(
        name: "meepleai.2fa.setup.total",
        unit: "setups",
        description: "Total number of 2FA setup operations");

    /// <summary>
    /// Counter for 2FA enable operations (verification + activation).
    /// Tracks successful 2FA enrollments.
    /// </summary>
    public static readonly Counter<long> TwoFactorEnableTotal = Meter.CreateCounter<long>(
        name: "meepleai.2fa.enable.total",
        unit: "enables",
        description: "Total number of 2FA enable operations");

    /// <summary>
    /// Counter for 2FA disable operations.
    /// Tracks when users disable 2FA (security posture monitoring).
    /// </summary>
    public static readonly Counter<long> TwoFactorDisableTotal = Meter.CreateCounter<long>(
        name: "meepleai.2fa.disable.total",
        unit: "disables",
        description: "Total number of 2FA disable operations");
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
        ArgumentNullException.ThrowIfNull(exception);
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

    /// <summary>
    /// Records LLM token usage following OpenTelemetry GenAI semantic conventions.
    /// Issue #1694: Track actual token usage from LLM calls with cost calculation.
    /// Issue #1725: Enhanced with per-user cost attribution (low-cardinality)
    /// </summary>
    /// <param name="promptTokens">Number of tokens in the prompt/input</param>
    /// <param name="completionTokens">Number of tokens in the completion/output</param>
    /// <param name="totalTokens">Total tokens used</param>
    /// <param name="modelId">Model identifier (e.g., "openai/gpt-4o-mini")</param>
    /// <param name="provider">Provider name (e.g., "OpenRouter", "Ollama")</param>
    /// <param name="operationDurationMs">Optional LLM operation duration in milliseconds</param>
    /// <param name="costUsd">Optional estimated cost in USD</param>
    /// <param name="userSegment">Optional user segment (free, pro, enterprise, admin) - LOW CARDINALITY</param>
    /// <param name="userIdHash">Optional user ID hash (first 8 chars) - LOW CARDINALITY for privacy</param>
    public static void RecordLlmTokenUsage(
        int promptTokens,
        int completionTokens,
        int totalTokens,
        string modelId,
        string provider,
        double? operationDurationMs = null,
        decimal? costUsd = null,
        string? userSegment = null,
        string? userIdHash = null)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(provider);
        // OpenTelemetry GenAI Semantic Convention: gen_ai.client.token.usage
        var baseTags = new TagList
        {
            { "gen_ai.request.model", modelId },
            { "gen_ai.response.model", modelId },
            { "gen_ai.system", provider.ToLowerInvariant() }
        };

        // Record prompt tokens
        var promptTags = baseTags;
        promptTags.Add("gen_ai.token.type", "input");
        GenAiTokenUsage.Add(promptTokens, promptTags);

        // Record completion tokens
        var completionTags = baseTags;
        completionTags.Add("gen_ai.token.type", "output");
        GenAiTokenUsage.Add(completionTokens, completionTags);

        // Record total tokens (for backward compatibility with existing TokensUsed metric)
        TokensUsed.Record(totalTokens, baseTags);

        // Record operation duration if provided
        if (operationDurationMs.HasValue)
        {
            var durationTags = new TagList
            {
                { "gen_ai.request.model", modelId },
                { "gen_ai.operation.name", "chat" },
                { "gen_ai.system", provider.ToLowerInvariant() }
            };
            GenAiOperationDuration.Record(operationDurationMs.Value, durationTags);
        }

        // Record cost if provided
        if (costUsd.HasValue)
        {
            var costTags = new TagList
            {
                { "model_id", modelId },
                { "provider", provider.ToLowerInvariant() }
            };

            // ISSUE-1725: Add user attribution tags (low-cardinality for Prometheus)
            if (!string.IsNullOrEmpty(userSegment))
            {
                costTags.Add("user_segment", userSegment);
            }

            if (!string.IsNullOrEmpty(userIdHash))
            {
                costTags.Add("user_id_hash", userIdHash);
            }

            LlmCostUsd.Record((double)costUsd.Value, costTags);
        }
    }

    /// <summary>
    /// Records agent invocation with token usage and cost tracking.
    /// Issue #1694: Enhanced agent metrics with LLM token consumption and cost attribution.
    /// </summary>
    /// <param name="agentType">Agent type (e.g., "RagAgent", "CitationAgent")</param>
    /// <param name="tokenUsage">Token usage information from LLM call</param>
    /// <param name="durationMs">Agent invocation duration in milliseconds</param>
    /// <param name="success">Whether the invocation succeeded</param>
    public static void RecordAgentInvocationWithTokens(
        string agentType,
        BoundedContexts.KnowledgeBase.Domain.ValueObjects.TokenUsage tokenUsage,
        double durationMs,
        bool success = true)
    {
        // Record standard agent invocation metrics
        RecordAgentInvocation(agentType, durationMs, success);

        // Record agent-specific token usage
        var tokenTags = new TagList
        {
            { "agent_type", agentType.ToLowerInvariant() },
            { "model_id", tokenUsage.ModelId },
            { "provider", tokenUsage.Provider.ToLowerInvariant() }
        };

        AgentTokenUsage.Add(tokenUsage.TotalTokens, tokenTags);

        // Record agent-specific cost
        AgentCostUsd.Record((double)tokenUsage.EstimatedCost, tokenTags);

        // Also record LLM-level metrics with OpenTelemetry GenAI conventions
        RecordLlmTokenUsage(
            promptTokens: tokenUsage.PromptTokens,
            completionTokens: tokenUsage.CompletionTokens,
            totalTokens: tokenUsage.TotalTokens,
            modelId: tokenUsage.ModelId,
            provider: tokenUsage.Provider,
            operationDurationMs: durationMs,
            costUsd: tokenUsage.EstimatedCost);
    }


    /// <summary>
    /// Records a 2FA verification attempt (TOTP or backup code).
    /// Tracks success/failure metrics and security events for Issue #1788 (SEC-08).
    /// </summary>
    /// <param name="verificationType">Type of verification: "totp" or "backup_code"</param>
    /// <param name="success">Whether the verification succeeded</param>
    /// <param name="userId">Optional user ID for granular tracking (should be hashed in production)</param>
    /// <param name="isReplayAttack">Whether this was a detected replay attack (TOTP only)</param>
    public static void Record2FAVerification(
        string verificationType,
        bool success,
        string? userId = null,
        bool isReplayAttack = false)
    {
        var tags = new TagList
        {
            { "verification_type", verificationType.ToLowerInvariant() },
            { "success", success }
        };

        if (!string.IsNullOrWhiteSpace(userId))
        {
            // Note: In production, consider hashing userId for GDPR compliance
            tags.Add("user_id", userId);
        }

        if (string.Equals(verificationType.ToLowerInvariant(), "totp", StringComparison.Ordinal))
        {
            if (isReplayAttack)
            {
                TwoFactorReplayAttacksBlocked.Add(1, tags);
            }
            else if (success)
            {
                TwoFactorSuccessfulTotpVerifications.Add(1, tags);
            }
            else
            {
                TwoFactorFailedTotpAttempts.Add(1, tags);
            }
        }
        else if (string.Equals(verificationType.ToLowerInvariant(), "backup_code", StringComparison.Ordinal))
        {
            if (success)
            {
                TwoFactorSuccessfulBackupCodeUses.Add(1, tags);
            }
            else
            {
                TwoFactorFailedBackupAttempts.Add(1, tags);
            }
        }
    }

    /// <summary>
    /// Records a 2FA lifecycle operation (setup, enable, disable).
    /// Tracks user adoption and security posture metrics for Issue #1788 (SEC-08).
    /// </summary>
    /// <param name="operation">Operation type: "setup", "enable", or "disable"</param>
    /// <param name="userId">Optional user ID for audit trail (should be hashed in production)</param>
    public static void Record2FALifecycle(string operation, string? userId = null)
    {
        var tags = new TagList { { "operation", operation.ToLowerInvariant() } };

        if (!string.IsNullOrWhiteSpace(userId))
        {
            tags.Add("user_id", userId);
        }

        switch (operation.ToLowerInvariant())
        {
            case "setup":
                TwoFactorSetupTotal.Add(1, tags);
                break;
            case "enable":
                TwoFactorEnableTotal.Add(1, tags);
                break;
            case "disable":
                TwoFactorDisableTotal.Add(1, tags);
                break;
        }
    }
    /// <summary>
    /// Counter for total evaluation runs.
    /// </summary>
    public static readonly Counter<long> EvaluationRunsTotal = Meter.CreateCounter<long>(
        name: "meepleai.evaluation.runs.total",
        unit: "runs",
        description: "Total number of RAG evaluation runs");

    /// <summary>
    /// Histogram for Recall@5 scores from evaluation.
    /// </summary>
    public static readonly Histogram<double> EvaluationRecallAt5 = Meter.CreateHistogram<double>(
        name: "meepleai.evaluation.recall_at_5",
        unit: "score",
        description: "Recall@5 scores from RAG evaluation");

    /// <summary>
    /// Histogram for Recall@10 scores from evaluation.
    /// </summary>
    public static readonly Histogram<double> EvaluationRecallAt10 = Meter.CreateHistogram<double>(
        name: "meepleai.evaluation.recall_at_10",
        unit: "score",
        description: "Recall@10 scores from RAG evaluation");

    /// <summary>
    /// Histogram for nDCG@10 scores from evaluation.
    /// </summary>
    public static readonly Histogram<double> EvaluationNdcgAt10 = Meter.CreateHistogram<double>(
        name: "meepleai.evaluation.ndcg_at_10",
        unit: "score",
        description: "nDCG@10 scores from RAG evaluation");

    /// <summary>
    /// Histogram for MRR (Mean Reciprocal Rank) scores from evaluation.
    /// </summary>
    public static readonly Histogram<double> EvaluationMrr = Meter.CreateHistogram<double>(
        name: "meepleai.evaluation.mrr",
        unit: "score",
        description: "Mean Reciprocal Rank scores from RAG evaluation");

    /// <summary>
    /// Histogram for P95 latency from evaluation runs.
    /// </summary>
    public static readonly Histogram<double> EvaluationP95Latency = Meter.CreateHistogram<double>(
        name: "meepleai.evaluation.p95_latency",
        unit: "ms",
        description: "P95 latency from RAG evaluation runs");

    /// <summary>
    /// Counter for configurations meeting Phase 5 target.
    /// </summary>
    public static readonly Counter<long> EvaluationTargetMet = Meter.CreateCounter<long>(
        name: "meepleai.evaluation.target_met.total",
        unit: "configurations",
        description: "Configurations meeting Phase 5 targets (Recall@10 >= 70%, P95 < 1.5s)");

    /// <summary>
    /// Counter for evaluation sample results by outcome.
    /// </summary>
    public static readonly Counter<long> EvaluationSamplesTotal = Meter.CreateCounter<long>(
        name: "meepleai.evaluation.samples.total",
        unit: "samples",
        description: "Total evaluation samples by outcome (hit/miss)");

    /// <summary>
    /// Histogram for grid search total duration.
    /// </summary>
    public static readonly Histogram<double> GridSearchDuration = Meter.CreateHistogram<double>(
        name: "meepleai.gridsearch.duration",
        unit: "ms",
        description: "Total duration of grid search runs");

    /// <summary>
    /// Counter for grid search configurations evaluated.
    /// </summary>
    public static readonly Counter<long> GridSearchConfigsEvaluated = Meter.CreateCounter<long>(
        name: "meepleai.gridsearch.configs_evaluated.total",
        unit: "configs",
        description: "Total configurations evaluated in grid search");

    /// <summary>
    /// Records a RAG evaluation run with all metrics.
    /// ADR-016 Phase 5: Captures comprehensive evaluation metrics for observability.
    /// </summary>
    /// <param name="recallAt5">Recall@5 score (0.0 to 1.0)</param>
    /// <param name="recallAt10">Recall@10 score (0.0 to 1.0)</param>
    /// <param name="ndcgAt10">nDCG@10 score (0.0 to 1.0)</param>
    /// <param name="mrr">Mean Reciprocal Rank (0.0 to 1.0)</param>
    /// <param name="p95LatencyMs">P95 latency in milliseconds</param>
    /// <param name="configurationId">Optional configuration identifier</param>
    /// <param name="datasetName">Optional dataset name</param>
    /// <param name="meetsTarget">Whether configuration meets Phase 5 targets</param>
    public static void RecordEvaluationRun(
        double recallAt5,
        double recallAt10,
        double ndcgAt10,
        double mrr,
        double p95LatencyMs,
        string? configurationId = null,
        string? datasetName = null,
        bool meetsTarget = false)
    {
        var tags = new TagList();

        if (!string.IsNullOrEmpty(configurationId))
        {
            tags.Add("configuration", configurationId);
        }

        if (!string.IsNullOrEmpty(datasetName))
        {
            tags.Add("dataset", datasetName);
        }

        EvaluationRunsTotal.Add(1, tags);
        EvaluationRecallAt5.Record(recallAt5, tags);
        EvaluationRecallAt10.Record(recallAt10, tags);
        EvaluationNdcgAt10.Record(ndcgAt10, tags);
        EvaluationMrr.Record(mrr, tags);
        EvaluationP95Latency.Record(p95LatencyMs, tags);

        if (meetsTarget)
        {
            EvaluationTargetMet.Add(1, tags);
        }
    }

    /// <summary>
    /// Records grid search completion with summary metrics.
    /// ADR-016 Phase 5: Tracks grid search runs for performance monitoring.
    /// </summary>
    /// <param name="totalDurationMs">Total grid search duration in milliseconds</param>
    /// <param name="configsEvaluated">Number of configurations evaluated</param>
    /// <param name="successfulConfigs">Number of successful evaluations</param>
    /// <param name="configsMeetingTarget">Number of configurations meeting Phase 5 target</param>
    /// <param name="datasetName">Dataset used for evaluation</param>
    public static void RecordGridSearchCompletion(
        double totalDurationMs,
        int configsEvaluated,
        int successfulConfigs,
        int configsMeetingTarget,
        string? datasetName = null)
    {
        var tags = new TagList
        {
            { "successful_count", successfulConfigs },
            { "target_met_count", configsMeetingTarget }
        };

        if (!string.IsNullOrEmpty(datasetName))
        {
            tags.Add("dataset", datasetName);
        }

        GridSearchDuration.Record(totalDurationMs, tags);
        GridSearchConfigsEvaluated.Add(configsEvaluated, tags);
    }

    /// <summary>
    /// Records individual evaluation sample outcome.
    /// ADR-016 Phase 5: Granular tracking of sample-level results.
    /// </summary>
    /// <param name="isHit">Whether the sample was a hit (relevant document in top-K)</param>
    /// <param name="rank">Rank of first relevant document (0 if miss)</param>
    /// <param name="configurationId">Configuration identifier</param>
    public static void RecordEvaluationSample(
        bool isHit,
        int rank = 0,
        string? configurationId = null)
    {
        var tags = new TagList
        {
            { "outcome", isHit ? "hit" : "miss" }
        };

        if (!string.IsNullOrEmpty(configurationId))
        {
            tags.Add("configuration", configurationId);
        }

        if (isHit && rank > 0)
        {
            tags.Add("rank", rank);
        }

        EvaluationSamplesTotal.Add(1, tags);
    }

    #region Notification Metrics

    /// <summary>
    /// Counter for total notifications created.
    /// Issue #2157: Tracks notification creation for operational visibility.
    /// </summary>
    public static readonly Counter<long> NotificationsCreatedTotal = Meter.CreateCounter<long>(
        name: "meepleai.notifications.created.total",
        unit: "notifications",
        description: "Total notifications created");

    /// <summary>
    /// Counter for notifications marked as read (single).
    /// Issue #2157: Tracks mark-as-read operations.
    /// </summary>
    public static readonly Counter<long> NotificationsReadTotal = Meter.CreateCounter<long>(
        name: "meepleai.notifications.read.total",
        unit: "notifications",
        description: "Total notifications marked as read");

    /// <summary>
    /// Counter for bulk mark-all-as-read operations.
    /// Issue #2157: Tracks batch read operations.
    /// </summary>
    public static readonly Counter<long> NotificationsMarkAllReadTotal = Meter.CreateCounter<long>(
        name: "meepleai.notifications.mark_all_read.total",
        unit: "operations",
        description: "Total mark-all-as-read operations");

    /// <summary>
    /// Histogram for mark-as-read operation duration.
    /// Issue #2157: Tracks latency for notification operations.
    /// </summary>
    public static readonly Histogram<double> NotificationMarkReadDuration = Meter.CreateHistogram<double>(
        name: "meepleai.notifications.mark_read.duration",
        unit: "ms",
        description: "Duration of mark-as-read operations");

    /// <summary>
    /// Histogram for mark-all-as-read operation duration.
    /// Issue #2157: Tracks latency for bulk notification operations.
    /// </summary>
    public static readonly Histogram<double> NotificationMarkAllReadDuration = Meter.CreateHistogram<double>(
        name: "meepleai.notifications.mark_all_read.duration",
        unit: "ms",
        description: "Duration of mark-all-as-read operations");

    /// <summary>
    /// Records a notification creation event.
    /// Issue #2157: Helper method for notification metrics.
    /// </summary>
    /// <param name="notificationType">Type of notification (e.g., system, game, user)</param>
    /// <param name="severity">Severity level (e.g., info, warning, error)</param>
    public static void RecordNotificationCreated(string notificationType, string severity)
    {
        var tags = new TagList
        {
            { "type", notificationType.ToLowerInvariant() },
            { "severity", severity.ToLowerInvariant() }
        };

        NotificationsCreatedTotal.Add(1, tags);
    }

    /// <summary>
    /// Records a mark-as-read operation.
    /// Issue #2157: Helper method for notification read metrics.
    /// </summary>
    /// <param name="durationMs">Duration of the operation in milliseconds</param>
    /// <param name="notificationType">Optional type of notification</param>
    public static void RecordNotificationRead(double durationMs, string? notificationType = null)
    {
        var tags = new TagList();

        if (!string.IsNullOrEmpty(notificationType))
        {
            tags.Add("type", notificationType.ToLowerInvariant());
        }

        NotificationsReadTotal.Add(1, tags);
        NotificationMarkReadDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records a mark-all-as-read operation.
    /// Issue #2157: Helper method for bulk notification read metrics.
    /// </summary>
    /// <param name="durationMs">Duration of the operation in milliseconds</param>
    /// <param name="notificationCount">Number of notifications marked as read</param>
    public static void RecordNotificationMarkAllRead(double durationMs, int notificationCount)
    {
        var tags = new TagList
        {
            { "count_bucket", GetCountBucket(notificationCount) }
        };

        NotificationsMarkAllReadTotal.Add(1, tags);
        NotificationMarkAllReadDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Gets a bucket label for notification count ranges.
    /// </summary>
    private static string GetCountBucket(int count)
    {
        return count switch
        {
            0 => "zero",
            <= 5 => "1-5",
            <= 20 => "6-20",
            <= 50 => "21-50",
            _ => "50+"
        };
    }

    #endregion
}
