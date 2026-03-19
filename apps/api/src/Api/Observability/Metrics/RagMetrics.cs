// OPS-02: RAG / KnowledgeBase / AI / Streaming / Agent Metrics
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    // ── RAG Core ──────────────────────────────────────────────────────────────

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
    /// Histogram for RAG Time-To-First-Token (TTFT) in milliseconds.
    /// Issue #5541: SLO metric — measures from request start to first LLM token.
    /// Includes embedding + vector search + prompt building + LLM first token.
    /// </summary>
    public static readonly Histogram<double> RagFirstTokenLatency = Meter.CreateHistogram<double>(
        name: "meepleai.rag.first_token_latency",
        unit: "ms",
        description: "RAG Time-To-First-Token latency in milliseconds (request start → first LLM response)");

    /// <summary>
    /// Counter for RAG errors by type
    /// </summary>
    public static readonly Counter<long> RagErrorsTotal = Meter.CreateCounter<long>(
        name: "meepleai.rag.errors.total",
        unit: "errors",
        description: "Total number of RAG errors by type");

    // ── OpenTelemetry GenAI Semantic Conventions ──────────────────────────────

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

    // ── Vector Search ─────────────────────────────────────────────────────────

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

    // ── Streaming ─────────────────────────────────────────────────────────────

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

    // ── Agents ────────────────────────────────────────────────────────────────

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

    // ── Hybrid Search ─────────────────────────────────────────────────────────

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

    // ── RAG Evaluation & Grid Search (ADR-016 Phase 5) ────────────────────────

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

    // ── AI Insights (Issue #4124) ─────────────────────────────────────────────

    /// <summary>
    /// Counter for total insight generation requests.
    /// Issue #4124: Tracks dashboard insights endpoint usage.
    /// </summary>
    public static readonly Counter<long> InsightGenerationTotal = Meter.CreateCounter<long>(
        name: "meepleai.insights.generation.total",
        unit: "requests",
        description: "Total number of insight generation requests");

    /// <summary>
    /// Histogram for insight generation duration in milliseconds.
    /// Issue #4124: Tracks P95 latency for SLO compliance (&lt;1s target).
    /// </summary>
    public static readonly Histogram<double> InsightGenerationDuration = Meter.CreateHistogram<double>(
        name: "meepleai.insights.generation.duration",
        unit: "ms",
        description: "Insight generation duration in milliseconds");

    /// <summary>
    /// Counter for insight generation errors by analyzer type.
    /// Issue #4124: Tracks failures per analyzer for root cause analysis.
    /// </summary>
    public static readonly Counter<long> InsightGenerationErrors = Meter.CreateCounter<long>(
        name: "meepleai.insights.generation.errors",
        unit: "errors",
        description: "Insight generation errors by analyzer type");

    /// <summary>
    /// Histogram for per-analyzer execution duration in milliseconds.
    /// Issue #4124: Identifies slow analyzers for performance optimization.
    /// </summary>
    public static readonly Histogram<double> InsightAnalyzerDuration = Meter.CreateHistogram<double>(
        name: "meepleai.insights.analyzer.duration",
        unit: "ms",
        description: "Per-analyzer execution duration in milliseconds");

    /// <summary>
    /// Counter for insights generated by type.
    /// Issue #4124: Tracks distribution of insight types.
    /// </summary>
    public static readonly Counter<long> InsightsGeneratedByType = Meter.CreateCounter<long>(
        name: "meepleai.insights.generated.total",
        unit: "insights",
        description: "Total insights generated by type");

    /// <summary>
    /// Counter for insight cache hits.
    /// Issue #4124: Tracks cache effectiveness for insights endpoint.
    /// </summary>
    public static readonly Counter<long> InsightCacheHits = Meter.CreateCounter<long>(
        name: "meepleai.insights.cache.hits",
        unit: "hits",
        description: "Insight cache hits");

    /// <summary>
    /// Counter for insight cache misses.
    /// Issue #4124: Tracks cache miss rate for insights endpoint.
    /// </summary>
    public static readonly Counter<long> InsightCacheMisses = Meter.CreateCounter<long>(
        name: "meepleai.insights.cache.misses",
        unit: "misses",
        description: "Insight cache misses");

    /// <summary>
    /// Counter for insight feedback submissions.
    /// Issue #4124: Tracks user feedback for accuracy measurement.
    /// </summary>
    public static readonly Counter<long> InsightFeedbackTotal = Meter.CreateCounter<long>(
        name: "meepleai.insights.feedback.total",
        unit: "submissions",
        description: "Total insight feedback submissions");

    /// <summary>
    /// Counter for insight performance degradation events.
    /// Issue #4124: Fires when generation exceeds 1000ms threshold.
    /// Used by Prometheus alerting to trigger performance alerts.
    /// </summary>
    public static readonly Counter<long> InsightPerformanceDegraded = Meter.CreateCounter<long>(
        name: "meepleai.insights.performance.degraded",
        unit: "events",
        description: "Insight generation exceeded performance threshold (1000ms)");

    /// <summary>
    /// Performance threshold for insight generation in milliseconds.
    /// Issue #4124: DoD target is response time &lt;1s (P95).
    /// </summary>
    public const double InsightPerformanceThresholdMs = 1000.0;

    // ── LLM Operational Maturity (Issue #5480) ────────────────────────────────

    /// <summary>
    /// Gauge for circuit breaker state per provider.
    /// Issue #5480: 0=Closed (healthy), 1=Open (blocked), 2=HalfOpen (testing).
    /// </summary>
    public static readonly UpDownCounter<int> CircuitBreakerState = Meter.CreateUpDownCounter<int>(
        name: "meepleai.llm.circuit_breaker_state",
        unit: "{state}",
        description: "Circuit breaker state per LLM provider (0=closed, 1=open, 2=halfopen)");

    /// <summary>
    /// Gauge for OpenRouter account balance in USD.
    /// Issue #5480: Scraped from /auth/key — enables budget alerting.
    /// </summary>
    public static readonly UpDownCounter<double> OpenRouterBalanceUsd = Meter.CreateUpDownCounter<double>(
        name: "meepleai.openrouter.balance_usd",
        unit: "USD",
        description: "OpenRouter account balance in USD");

    /// <summary>
    /// Gauge for OpenRouter RPM utilization (0.0 to 1.0).
    /// Issue #5480: Current RPM / Limit RPM — enables threshold alerting.
    /// </summary>
    public static readonly UpDownCounter<double> OpenRouterRpmUtilization = Meter.CreateUpDownCounter<double>(
        name: "meepleai.openrouter.rpm_utilization",
        unit: "ratio",
        description: "OpenRouter RPM utilization ratio (0.0-1.0)");

    /// <summary>
    /// Counter for total LLM cost in USD per provider and model.
    /// Issue #5480: Aggregated cost tracking for billing dashboards.
    /// </summary>
    public static readonly Counter<double> LlmCostUsdTotal = Meter.CreateCounter<double>(
        name: "meepleai.llm.cost_usd_total",
        unit: "USD",
        description: "Total LLM cost in USD by provider and model");

    /// <summary>
    /// Counter for total LLM requests by provider, model, and status.
    /// Issue #5480: Enables per-provider request volume and error rate tracking.
    /// </summary>
    public static readonly Counter<long> LlmRequestsTotal = Meter.CreateCounter<long>(
        name: "meepleai.llm.requests_total",
        unit: "requests",
        description: "Total LLM requests by provider, model, and status");

    /// <summary>
    /// Histogram for LLM request latency in seconds by provider and model.
    /// Issue #5480: Enables P50/P95/P99 latency dashboards per provider.
    /// </summary>
    public static readonly Histogram<double> LlmLatencySeconds = Meter.CreateHistogram<double>(
        name: "meepleai.llm.latency_seconds",
        unit: "s",
        description: "LLM request latency in seconds by provider and model");

    // ── RAG Retrieval Quality ─────────────────────────────────────────────────

    public static readonly Histogram<int> RagRetrievalChunkCount = Meter.CreateHistogram<int>(
        name: "meepleai.rag.retrieval.chunk_count",
        unit: "chunks",
        description: "Number of chunks returned per RAG retrieval");

    public static readonly Histogram<double> RagRetrievalAvgScore = Meter.CreateHistogram<double>(
        name: "meepleai.rag.retrieval.avg_score",
        unit: "score",
        description: "Average similarity score of retrieved chunks");

    public static readonly Counter<long> RagEnhancementActivations = Meter.CreateCounter<long>(
        name: "meepleai.rag.enhancement.activations",
        unit: "activations",
        description: "RAG enhancement activations by type");

    public static readonly Counter<long> RagRetrievalFallbacks = Meter.CreateCounter<long>(
        name: "meepleai.rag.retrieval.fallbacks",
        unit: "fallbacks",
        description: "RAG retrieval fallback events by type");

    public static readonly Counter<long> RagCragVerdicts = Meter.CreateCounter<long>(
        name: "meepleai.rag.crag.verdicts",
        unit: "evaluations",
        description: "CRAG retrieval evaluation verdicts");

    public static readonly Counter<long> RagAdaptiveRoutingDecisions = Meter.CreateCounter<long>(
        name: "meepleai.rag.adaptive.decisions",
        unit: "decisions",
        description: "Adaptive RAG routing decisions by complexity level");

    public static readonly Histogram<int> RagFusionQueryCount = Meter.CreateHistogram<int>(
        name: "meepleai.rag.fusion.query_count",
        unit: "queries",
        description: "Number of query variants generated by RAG-Fusion");

    // ── Helper Methods ────────────────────────────────────────────────────────

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

    /// <summary>
    /// Records insight generation with duration and analyzer breakdown.
    /// Issue #4124: Helper method for comprehensive insight metrics.
    /// </summary>
    /// <param name="totalDurationMs">Total generation duration in milliseconds</param>
    /// <param name="insightCount">Number of insights generated</param>
    /// <param name="cacheHit">Whether result came from cache</param>
    /// <param name="analyzerDurations">Optional per-analyzer durations (analyzer_name → duration_ms)</param>
    public static void RecordInsightGeneration(
        double totalDurationMs,
        int insightCount,
        bool cacheHit,
        IDictionary<string, double>? analyzerDurations = null)
    {
        var tags = new TagList
        {
            { "cache_hit", cacheHit }
        };

        InsightGenerationTotal.Add(1, tags);
        InsightGenerationDuration.Record(totalDurationMs, tags);

        if (cacheHit)
        {
            InsightCacheHits.Add(1);
        }
        else
        {
            InsightCacheMisses.Add(1);
        }

        if (analyzerDurations != null)
        {
            foreach (var (analyzer, durationMs) in analyzerDurations)
            {
                var analyzerTags = new TagList { { "analyzer", analyzer.ToLowerInvariant() } };
                InsightAnalyzerDuration.Record(durationMs, analyzerTags);
            }
        }
    }

    /// <summary>
    /// Records insight feedback submission with relevance tracking.
    /// Issue #4124: Tracks feedback for accuracy calculation (target &gt;75%).
    /// </summary>
    /// <param name="insightType">Type of insight that received feedback</param>
    /// <param name="isRelevant">Whether user marked the insight as relevant</param>
    public static void RecordInsightFeedback(string insightType, bool isRelevant)
    {
        var tags = new TagList
        {
            { "insight_type", insightType.ToLowerInvariant() },
            { "relevant", isRelevant }
        };

        InsightFeedbackTotal.Add(1, tags);
    }

    /// <summary>
    /// Records a circuit breaker state change for Prometheus export.
    /// Issue #5480: Called from CircuitBreakerState.OnStateTransition callback.
    /// </summary>
    public static void RecordCircuitBreakerState(string provider, int stateValue)
    {
        // Reset to 0 and then set to new value using UpDownCounter
        // Since this is a gauge-like metric, consumers should take the last value
        CircuitBreakerState.Add(stateValue, new TagList { { "provider", provider } });
    }

    /// <summary>
    /// Records an LLM request with provider, model, and status for Prometheus export.
    /// Issue #5480: Called from HybridLlmService after each completion.
    /// </summary>
    public static void RecordLlmRequest(
        string provider,
        string model,
        string status,
        double latencySeconds,
        double? costUsd = null)
    {
        var tags = new TagList
        {
            { "provider", provider },
            { "model", model },
            { "status", status }
        };

        LlmRequestsTotal.Add(1, tags);
        LlmLatencySeconds.Record(latencySeconds, tags);

        if (costUsd.HasValue && costUsd.Value > 0)
        {
            LlmCostUsdTotal.Add(costUsd.Value, new TagList
            {
                { "provider", provider },
                { "model", model }
            });
        }
    }

    /// <summary>
    /// Records OpenRouter operational gauges for Prometheus export.
    /// Issue #5480: Called from OpenRouterUsageService polling cycle.
    /// </summary>
    public static void RecordOpenRouterOperationalState(
        double balanceUsd,
        double rpmUtilization)
    {
        OpenRouterBalanceUsd.Add(balanceUsd, new TagList { { "provider", "openrouter" } });
        OpenRouterRpmUtilization.Add(rpmUtilization, new TagList { { "provider", "openrouter" } });
    }
}
