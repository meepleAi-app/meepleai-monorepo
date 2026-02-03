using Api.BoundedContexts.SystemConfiguration.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository for tier-strategy access records.
/// Issue #3436: Part of tier-strategy-model architecture.
/// </summary>
public interface ITierStrategyAccessRepository
{
    /// <summary>
    /// Gets all enabled strategy names for a specific tier.
    /// Includes wildcard ("*") strategies if any.
    /// </summary>
    /// <param name="tier">The user tier to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of enabled strategy names.</returns>
    Task<IReadOnlyList<string>> GetEnabledStrategiesForTierAsync(LlmUserTier tier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific tier-strategy combination is enabled.
    /// Also checks for wildcard access ("*" strategy).
    /// </summary>
    /// <param name="tier">The user tier.</param>
    /// <param name="strategy">The strategy name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if access is enabled.</returns>
    Task<bool> IsAccessEnabledAsync(LlmUserTier tier, string strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tier-strategy access records for a tier.
    /// </summary>
    /// <param name="tier">The user tier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tier-strategy access entries.</returns>
    Task<IReadOnlyList<TierStrategyAccessEntry>> GetAllForTierAsync(LlmUserTier tier, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read model for tier-strategy access entry.
/// </summary>
/// <param name="Tier">The user tier name.</param>
/// <param name="Strategy">The strategy name.</param>
/// <param name="IsEnabled">Whether access is enabled.</param>
public record TierStrategyAccessEntry(string Tier, string Strategy, bool IsEnabled);
