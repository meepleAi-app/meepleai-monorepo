using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Service for validating user tier access to RAG strategies.
/// Issue #3436: Part of tier-strategy-model architecture.
/// </summary>
/// <remarks>
/// Implements the principle: "Tier determines strategy ACCESS (not model selection)"
/// </remarks>
public interface ITierStrategyAccessService
{
    /// <summary>
    /// Checks if a user tier has access to a specific strategy.
    /// </summary>
    /// <param name="tier">The user's tier.</param>
    /// <param name="strategy">The requested strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the tier has access to the strategy.</returns>
    Task<bool> HasAccessToStrategyAsync(LlmUserTier tier, RagStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all strategies available to a user tier.
    /// </summary>
    /// <param name="tier">The user's tier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available strategies for the tier.</returns>
    Task<IReadOnlyList<RagStrategy>> GetAvailableStrategiesAsync(LlmUserTier tier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a user's access to a strategy with detailed result.
    /// </summary>
    /// <param name="tier">The user's tier.</param>
    /// <param name="strategy">The requested strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with details.</returns>
    Task<TierStrategyValidationResult> ValidateAccessAsync(LlmUserTier tier, RagStrategy strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default strategy for a user tier.
    /// </summary>
    /// <param name="tier">The user's tier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The default strategy for the tier, or null if no strategies available.</returns>
    Task<RagStrategy?> GetDefaultStrategyAsync(LlmUserTier tier, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of tier-strategy access validation.
/// </summary>
/// <param name="IsValid">Whether the tier has access to the strategy.</param>
/// <param name="Tier">The user's tier.</param>
/// <param name="Strategy">The requested strategy.</param>
/// <param name="Message">Validation message with details.</param>
/// <param name="AvailableStrategies">Strategies available to the tier (populated on failure).</param>
public record TierStrategyValidationResult(
    bool IsValid,
    LlmUserTier Tier,
    RagStrategy Strategy,
    string Message,
    IReadOnlyList<RagStrategy>? AvailableStrategies = null)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static TierStrategyValidationResult Success(LlmUserTier tier, RagStrategy strategy)
        => new(true, tier, strategy, "Access granted");

    /// <summary>
    /// Creates a failed validation result for insufficient tier access.
    /// </summary>
    public static TierStrategyValidationResult AccessDenied(
        LlmUserTier tier,
        RagStrategy strategy,
        IReadOnlyList<RagStrategy> availableStrategies)
        => new(
            false,
            tier,
            strategy,
            $"Access denied: Tier '{tier}' cannot access strategy '{strategy.GetDisplayName()}'. " +
            $"Available strategies: {FormatStrategies(availableStrategies)}",
            availableStrategies);

    /// <summary>
    /// Creates a failed validation result for no strategies available.
    /// </summary>
    public static TierStrategyValidationResult NoStrategiesAvailable(LlmUserTier tier, RagStrategy strategy)
        => new(
            false,
            tier,
            strategy,
            $"Access denied: Tier '{tier}' has no available strategies",
            Array.Empty<RagStrategy>());

    /// <summary>
    /// Creates a failed validation result for strategy not found/disabled.
    /// </summary>
    public static TierStrategyValidationResult StrategyDisabled(LlmUserTier tier, RagStrategy strategy)
        => new(
            false,
            tier,
            strategy,
            $"Strategy '{strategy.GetDisplayName()}' is currently disabled");

    private static string FormatStrategies(IReadOnlyList<RagStrategy> strategies)
        => strategies.Count == 0
            ? "none"
            : string.Join(", ", strategies.Select(s => s.GetDisplayName()));
}
