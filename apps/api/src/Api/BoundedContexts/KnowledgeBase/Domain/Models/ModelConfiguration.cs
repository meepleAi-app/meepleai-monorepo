using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

/// <summary>
/// Complete model configuration including pricing and tier information.
/// Issue #3377: Models Tier Endpoint
/// </summary>
/// <remarks>
/// Extends the pricing information with user-facing model metadata
/// and tier-based access control information.
/// </remarks>
public record ModelConfiguration
{
    /// <summary>
    /// Unique model identifier (e.g., "anthropic/claude-3.5-haiku", "openai/gpt-4o-mini").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable display name (e.g., "Claude 3.5 Haiku", "GPT-4o Mini").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// LLM provider (e.g., "anthropic", "openai", "meta-llama", "google").
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Required subscription tier to access this model.
    /// </summary>
    public required ModelTier Tier { get; init; }

    /// <summary>
    /// Cost per 1,000 input tokens in USD.
    /// </summary>
    public required decimal CostPer1kInputTokens { get; init; }

    /// <summary>
    /// Cost per 1,000 output tokens in USD.
    /// </summary>
    public required decimal CostPer1kOutputTokens { get; init; }

    /// <summary>
    /// Maximum tokens supported by this model.
    /// </summary>
    public required int MaxTokens { get; init; }

    /// <summary>
    /// Whether this model supports streaming responses.
    /// </summary>
    public required bool SupportsStreaming { get; init; }

    /// <summary>
    /// Optional description of model capabilities.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this model has zero cost (free tier or self-hosted).
    /// </summary>
    public bool IsFree => CostPer1kInputTokens == 0 && CostPer1kOutputTokens == 0;

    /// <summary>
    /// Creates a new ModelConfiguration with validation.
    /// </summary>
    public static ModelConfiguration Create(
        string id,
        string name,
        string provider,
        ModelTier tier,
        decimal costPer1kInput,
        decimal costPer1kOutput,
        int maxTokens,
        bool supportsStreaming = true,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Model ID cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Model name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider cannot be empty", nameof(provider));

        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Max tokens must be positive");

        if (costPer1kInput < 0)
            throw new ArgumentOutOfRangeException(nameof(costPer1kInput), "Cost cannot be negative");

        if (costPer1kOutput < 0)
            throw new ArgumentOutOfRangeException(nameof(costPer1kOutput), "Cost cannot be negative");

        return new ModelConfiguration
        {
            Id = id,
            Name = name,
            Provider = provider,
            Tier = tier,
            CostPer1kInputTokens = costPer1kInput,
            CostPer1kOutputTokens = costPer1kOutput,
            MaxTokens = maxTokens,
            SupportsStreaming = supportsStreaming,
            Description = description
        };
    }
}
