// OPS-02: Agent invocation, token usage, cost, and streaming metrics
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Histogram for AI tokens used per request
    /// </summary>
    public static readonly Histogram<long> TokensUsed = Meter.CreateHistogram<long>(
        name: "meepleai.ai.tokens.used",
        unit: "tokens",
        description: "AI tokens used per request");

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
}
