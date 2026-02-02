using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.TierAccess;

/// <summary>
/// Service for validating user tier access to RAG strategies.
/// Enforces the "Tier → Available Strategies" access control policy.
/// Issue #3436: TierStrategyAccess validation service.
/// </summary>
public interface ITierStrategyAccessService
{
    /// <summary>
    /// Checks if a user tier has access to a specific RAG strategy.
    /// </summary>
    /// <param name="tier">The user's subscription tier.</param>
    /// <param name="strategy">The RAG strategy to check access for.</param>
    /// <returns>True if the tier can access the strategy, false otherwise.</returns>
    Task<bool> HasAccessToStrategyAsync(UserTier tier, RagStrategy strategy);

    /// <summary>
    /// Gets the list of RAG strategies available to a user tier.
    /// </summary>
    /// <param name="tier">The user's subscription tier.</param>
    /// <returns>List of strategies the tier can access.</returns>
    Task<IReadOnlyList<RagStrategy>> GetAvailableStrategiesAsync(UserTier tier);

    /// <summary>
    /// Gets the complete tier access configuration including all permissions.
    /// </summary>
    /// <param name="tier">The user's subscription tier.</param>
    /// <returns>The tier's strategy access configuration.</returns>
    Task<TierStrategyAccess> GetTierAccessAsync(UserTier tier);

    /// <summary>
    /// Validates whether a user tier can access a specific strategy.
    /// Returns a detailed validation result with success/failure message.
    /// </summary>
    /// <param name="tier">The user's subscription tier.</param>
    /// <param name="strategy">The RAG strategy to validate access for.</param>
    /// <returns>Validation result with details.</returns>
    Task<TierStrategyValidationResult> ValidateAccessAsync(UserTier tier, RagStrategy strategy);
}
