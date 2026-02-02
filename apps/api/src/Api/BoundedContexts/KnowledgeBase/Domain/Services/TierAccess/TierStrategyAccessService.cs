using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.TierAccess;

/// <summary>
/// Implementation of tier-to-strategy access control service.
/// Enforces the access matrix:
/// - Free (User): Fast, Balanced
/// - Premium: Fast, Balanced, Precise (no Custom)
/// - Pro (Editor): Fast, Balanced, Precise
/// - Admin: Fast, Balanced, Precise, Custom (all strategies)
/// Issue #3436: TierStrategyAccess validation service.
/// </summary>
public sealed class TierStrategyAccessService : ITierStrategyAccessService
{
    private readonly ILogger<TierStrategyAccessService> _logger;

    /// <summary>
    /// Default access matrix mapping tiers to their available strategies.
    /// This can be extended to use database configuration via TierStrategyAccessEntity.
    /// </summary>
    private static readonly Dictionary<UserTier, TierStrategyAccess> DefaultAccessMatrix = new()
    {
        // Free tier (User): Basic strategies only
        [UserTier.Free] = ValueObjects.TierStrategyAccess.Create(
            UserTier.Free,
            new[] { RagStrategy.Fast, RagStrategy.Balanced },
            canAccessCustom: false),

        // Premium tier: All standard strategies, no custom
        [UserTier.Premium] = ValueObjects.TierStrategyAccess.Create(
            UserTier.Premium,
            new[] { RagStrategy.Fast, RagStrategy.Balanced, RagStrategy.Precise },
            canAccessCustom: false),

        // Pro tier (Editor): All standard strategies, no custom
        [UserTier.Pro] = ValueObjects.TierStrategyAccess.Create(
            UserTier.Pro,
            new[] { RagStrategy.Fast, RagStrategy.Balanced, RagStrategy.Precise },
            canAccessCustom: false),

        // Admin tier: Full access including custom strategies
        [UserTier.Admin] = ValueObjects.TierStrategyAccess.Create(
            UserTier.Admin,
            new[] { RagStrategy.Fast, RagStrategy.Balanced, RagStrategy.Precise, RagStrategy.Custom },
            canAccessCustom: true)
    };

    public TierStrategyAccessService(ILogger<TierStrategyAccessService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<bool> HasAccessToStrategyAsync(UserTier tier, RagStrategy strategy)
    {
        var tierAccess = GetTierAccessInternal(tier);
        var hasAccess = tierAccess.HasAccessTo(strategy);

        _logger.LogDebug(
            "Access check: Tier={Tier}, Strategy={Strategy}, HasAccess={HasAccess}",
            tier, strategy, hasAccess);

        return Task.FromResult(hasAccess);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RagStrategy>> GetAvailableStrategiesAsync(UserTier tier)
    {
        var tierAccess = GetTierAccessInternal(tier);

        _logger.LogDebug(
            "Getting available strategies for Tier={Tier}: {Strategies}",
            tier, string.Join(", ", tierAccess.AvailableStrategies));

        return Task.FromResult(tierAccess.AvailableStrategies);
    }

    /// <inheritdoc />
    public Task<TierStrategyAccess> GetTierAccessAsync(UserTier tier)
    {
        var tierAccess = GetTierAccessInternal(tier);

        _logger.LogDebug(
            "Getting tier access for Tier={Tier}: {TierAccess}",
            tier, tierAccess);

        return Task.FromResult(tierAccess);
    }

    /// <inheritdoc />
    public Task<TierStrategyValidationResult> ValidateAccessAsync(UserTier tier, RagStrategy strategy)
    {
        var tierAccess = GetTierAccessInternal(tier);

        if (!tierAccess.HasAnyAccess)
        {
            _logger.LogWarning(
                "Access validation failed: Tier={Tier} has no RAG strategy access",
                tier);
            return Task.FromResult(TierStrategyValidationResult.NoAccess(tier));
        }

        if (!tierAccess.HasAccessTo(strategy))
        {
            _logger.LogWarning(
                "Access validation failed: Tier={Tier} cannot access Strategy={Strategy}",
                tier, strategy);
            return Task.FromResult(TierStrategyValidationResult.AccessDenied(tier, strategy));
        }

        _logger.LogDebug(
            "Access validation succeeded: Tier={Tier}, Strategy={Strategy}",
            tier, strategy);
        return Task.FromResult(TierStrategyValidationResult.Success(tier, strategy));
    }

    /// <summary>
    /// Gets the tier access from the default matrix.
    /// Returns NoAccess for unknown tiers.
    /// </summary>
    private static TierStrategyAccess GetTierAccessInternal(UserTier tier) =>
        DefaultAccessMatrix.TryGetValue(tier, out var access)
            ? access
            : ValueObjects.TierStrategyAccess.NoAccess(tier);
}
