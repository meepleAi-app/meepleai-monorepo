using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;

namespace Api.BoundedContexts.SystemConfiguration.Application.Services;

/// <summary>
/// Service for LLM tier-based model routing.
/// Issue #2596: Migrate LLM tier routing to database with test/production model separation.
/// </summary>
/// <remarks>
/// This service provides cached access to tier-specific LLM model configurations.
/// It replaces the hardcoded appsettings.json LlmRouting configuration with database-driven
/// runtime configuration that can be changed without app restart.
///
/// Benefits:
/// - No app restart for model changes
/// - Separate test/production configurations
/// - A/B testing capability
/// - Runtime model rollback
/// - Cost tracking per tier
/// </remarks>
public interface ILlmTierRoutingService
{
    /// <summary>
    /// Get the model configuration for a specific user tier.
    /// Uses current environment (Production/Test) to select appropriate model.
    /// Results are cached with 5-minute TTL.
    /// </summary>
    /// <param name="tier">User tier (Anonymous, User, Editor, Admin, Premium)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Model configuration for the tier, or null if not configured</returns>
    Task<AiModelConfiguration?> GetModelForTierAsync(LlmUserTier tier, CancellationToken ct = default);

    /// <summary>
    /// Get the test model configuration for a specific user tier.
    /// Explicitly requests test environment model, ignoring current environment.
    /// </summary>
    /// <param name="tier">User tier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Test model configuration for the tier, or null if not configured</returns>
    Task<AiModelConfiguration?> GetTestModelForTierAsync(LlmUserTier tier, CancellationToken ct = default);

    /// <summary>
    /// Get all tier routing configurations for admin overview.
    /// Returns configurations grouped by tier and environment.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>All tier routing configurations</returns>
    Task<IReadOnlyList<TierRoutingInfo>> GetAllTierRoutingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Invalidate the tier routing cache.
    /// Call after updating tier routing configurations.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task InvalidateCacheAsync(CancellationToken ct = default);
}

/// <summary>
/// Tier routing information DTO for admin overview.
/// </summary>
public sealed record TierRoutingInfo
{
    /// <summary>User tier this configuration applies to.</summary>
    public required LlmUserTier Tier { get; init; }

    /// <summary>Production model ID.</summary>
    public string? ProductionModelId { get; init; }

    /// <summary>Production model display name.</summary>
    public string? ProductionModelName { get; init; }

    /// <summary>Production model provider (OpenRouter, Ollama).</summary>
    public string? ProductionProvider { get; init; }

    /// <summary>Test model ID.</summary>
    public string? TestModelId { get; init; }

    /// <summary>Test model display name.</summary>
    public string? TestModelName { get; init; }

    /// <summary>Test model provider (OpenRouter, Ollama).</summary>
    public string? TestProvider { get; init; }

    /// <summary>Estimated monthly cost in USD based on usage stats.</summary>
    public decimal EstimatedMonthlyCostUsd { get; init; }
}
