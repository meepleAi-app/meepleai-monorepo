namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing LLM token usage and cost for agent invocations.
/// Implements OpenTelemetry GenAI semantic conventions for observability.
/// </summary>
/// <remarks>
/// Issue #1694: Track actual token usage from LLM calls.
/// Follows OpenTelemetry GenAI Semantic Conventions:
/// - gen_ai.usage.prompt_tokens
/// - gen_ai.usage.completion_tokens
/// - gen_ai.usage.total_tokens
/// </remarks>
public sealed record TokenUsage
{
    /// <summary>
    /// Number of tokens in the prompt/input.
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// Number of tokens in the completion/output.
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Total tokens used (prompt + completion).
    /// </summary>
    public int TotalTokens { get; init; }

    /// <summary>
    /// Estimated cost in USD for this token usage.
    /// </summary>
    public decimal EstimatedCost { get; init; }

    /// <summary>
    /// Model used for generation (for cost calculation context).
    /// </summary>
    public string ModelId { get; init; }

    /// <summary>
    /// Provider name (OpenRouter, Ollama, etc.).
    /// </summary>
    public string Provider { get; init; }

    /// <summary>
    /// Creates a new TokenUsage instance.
    /// </summary>
    public TokenUsage(
        int promptTokens,
        int completionTokens,
        int totalTokens,
        decimal estimatedCost,
        string modelId,
        string provider)
    {
        if (promptTokens < 0)
            throw new ArgumentException("Prompt tokens cannot be negative", nameof(promptTokens));

        if (completionTokens < 0)
            throw new ArgumentException("Completion tokens cannot be negative", nameof(completionTokens));

        if (totalTokens < 0)
            throw new ArgumentException("Total tokens cannot be negative", nameof(totalTokens));

        if (estimatedCost < 0)
            throw new ArgumentException("Estimated cost cannot be negative", nameof(estimatedCost));

        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model ID cannot be empty", nameof(modelId));

        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider cannot be empty", nameof(provider));

        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = totalTokens;
        EstimatedCost = estimatedCost;
        ModelId = modelId.Trim();
        Provider = provider.Trim();
    }

    /// <summary>
    /// Empty token usage (no LLM call made).
    /// </summary>
    public static TokenUsage Empty { get; } = new TokenUsage(0, 0, 0, 0m, "none", "none");

    /// <summary>
    /// Creates TokenUsage from existing LlmUsage and LlmCost from Services namespace.
    /// </summary>
    public static TokenUsage FromLlmResult(
        Api.Services.LlmUsage usage,
        Api.Services.LlmCost cost)
    {
        return new TokenUsage(
            promptTokens: usage.PromptTokens,
            completionTokens: usage.CompletionTokens,
            totalTokens: usage.TotalTokens,
            estimatedCost: cost.TotalCost,
            modelId: cost.ModelId,
            provider: cost.Provider);
    }

    public override string ToString() =>
        $"TokenUsage(Total={TotalTokens}, Prompt={PromptTokens}, Completion={CompletionTokens}, Cost=${EstimatedCost:F6}, Model={ModelId}, Provider={Provider})";
}
