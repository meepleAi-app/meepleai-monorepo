namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

#pragma warning disable MA0048 // File name must match type name - intentional grouping of related playground models

/// <summary>
/// Extended Complete event with playground-specific metadata.
/// Issue #4392: Includes agent config snapshot and latency breakdown.
/// Issue #4437: Added strategy field.
/// Issue #4439: Added cost breakdown.
/// Issue #4441: Added strategy info with parameters.
/// Issue #4442: Added pipeline timings.
/// Issue #4443: Added cache info.
/// Issue #4444: Added API call traces.
/// Issue #4445: Added structured log entries.
/// Issue #4446: Added TOMAC-RAG layer visualization data.
/// Issue #4468: Added resolved system prompt for debug panel preview.
/// </summary>
internal record PlaygroundStreamingComplete(
    int estimatedReadingTimeMinutes,
    int promptTokens,
    int completionTokens,
    int totalTokens,
    double? confidence,
    string strategy,
    PlaygroundCostBreakdown costBreakdown,
    PlaygroundAgentConfigSnapshot agentConfig,
    PlaygroundLatencyBreakdown latencyBreakdown,
    PlaygroundStrategyInfo? strategyInfo = null,
    List<PlaygroundPipelineStep>? pipelineTimings = null,
    PlaygroundCacheInfo? cacheInfo = null,
    List<PlaygroundApiTrace>? apiTraces = null,
    List<PlaygroundLogEntry>? logEntries = null,
    List<PlaygroundTomacLayer>? tomacLayers = null,
    string? systemPrompt = null,
    PlaygroundPromptTemplateInfo? promptTemplateInfo = null,
    PlaygroundTierInfo? tierInfo = null,
    PlaygroundCostEstimate? costEstimate = null,
    List<PlaygroundDataFlowStep>? dataFlowSteps = null);

/// <summary>
/// Snapshot of the agent configuration used during playground chat.
/// </summary>
internal record PlaygroundAgentConfigSnapshot(
    string AgentName,
    string Model,
    float Temperature,
    int MaxTokens,
    string Provider,
    bool IsModelOverride = false);

/// <summary>
/// Cost breakdown for playground chat.
/// Issue #4439: Real cost tracking.
/// </summary>
internal record PlaygroundCostBreakdown(
    decimal llmCost,
    decimal inputCost,
    decimal outputCost,
    decimal totalCost,
    bool isFree);

/// <summary>
/// Latency breakdown for playground chat.
/// </summary>
internal record PlaygroundLatencyBreakdown(
    long totalMs,
    long retrievalMs,
    long generationMs);

/// <summary>
/// Strategy info snapshot for playground debug panel.
/// Issue #4441: Exposes strategy name, type, and parameters.
/// </summary>
internal record PlaygroundStrategyInfo(
    string name,
    string type,
    Dictionary<string, object> parameters);

/// <summary>
/// Individual pipeline step timing for waterfall visualization.
/// Issue #4442: Per-step timing with type categorization.
/// </summary>
internal record PlaygroundPipelineStep(
    string name,
    string type,
    long durationMs,
    string? detail);

/// <summary>
/// Cache observability info for playground debug panel.
/// Issue #4443: Track cache hit/miss per request.
/// </summary>
internal record PlaygroundCacheInfo(
    string status,
    string? tier,
    string? cacheKey,
    double latencyMs,
    int ttlSeconds);

/// <summary>
/// Structured log entry for developer console.
/// Issue #4445: Pipeline-level log with level/source metadata.
/// </summary>
internal record PlaygroundLogEntry(
    string level,
    string source,
    string message,
    DateTime timestamp);

/// <summary>
/// API call trace for playground network inspector.
/// Issue #4444: Track all external API calls with HTTP-level details.
/// </summary>
internal record PlaygroundApiTrace(
    string service,
    string method,
    string url,
    int requestSizeBytes,
    int responseSizeBytes,
    int statusCode,
    long latencyMs,
    string? detail,
    string? requestPreview,
    string? responsePreview);

/// <summary>
/// TOMAC-RAG layer visualization data for playground debug panel.
/// Issue #4446: Shows 6 TOMAC layers with implementation status and metrics.
/// </summary>
internal record PlaygroundTomacLayer(
    string id,
    string name,
    string status,
    long latencyMs,
    int itemsProcessed,
    double? score,
    string description);

/// <summary>
/// Prompt template metadata for debug panel.
/// Issue #4469: Shows which prompt template is active.
/// </summary>
internal record PlaygroundPromptTemplateInfo(
    string role,
    int promptCount,
    DateTime? lastModified);

/// <summary>
/// Tier information for strategy access display.
/// Issue #4471: Shows required tier vs user access.
/// </summary>
internal record PlaygroundTierInfo(
    string requiredTier,
    string userTier,
    bool hasAccess);

/// <summary>
/// Semantic data flow step capturing actual data at each pipeline stage.
/// Issue #4456: End-to-end data flow visualization.
/// </summary>
internal record PlaygroundDataFlowStep(
    string stepName,
    string stepType,
    string summary,
    Dictionary<string, string> details,
    List<PlaygroundDataFlowItem>? items = null);

/// <summary>
/// Sub-item within a data flow step (e.g., individual search result).
/// </summary>
internal record PlaygroundDataFlowItem(
    string label,
    double? score,
    string? preview);

/// <summary>
/// Pre-execution cost estimate range for a strategy.
/// Issue #4472: Shows expected cost before execution.
/// </summary>
internal record PlaygroundCostEstimate(
    decimal minCost,
    decimal maxCost,
    decimal inputPricePer1M,
    decimal outputPricePer1M,
    bool isFree);

/// <summary>
/// Internal cache entry for playground query deduplication.
/// Issue #4443: Simple in-memory cache with TTL.
/// </summary>
internal record PlaygroundCacheEntry(
    int ResultCount,
    DateTime CachedAt);
