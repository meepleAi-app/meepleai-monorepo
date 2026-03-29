using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing the RAG strategy access configuration for a user tier.
/// Contains the available strategies and access permissions for a specific tier.
/// Issue #3436: TierStrategyAccess validation service.
/// </summary>
public sealed record TierStrategyAccess
{
    /// <summary>
    /// The user tier this access configuration applies to.
    /// </summary>
    public LlmUserTier Tier { get; }

    /// <summary>
    /// The list of RAG strategies available to this tier.
    /// </summary>
    public IReadOnlyList<RagStrategy> AvailableStrategies { get; }

    /// <summary>
    /// Whether this tier can access custom strategy configurations.
    /// Only Admin tier has this permission.
    /// </summary>
    public bool CanAccessCustom { get; }

    /// <summary>
    /// Whether this tier has any strategy access at all.
    /// </summary>
    public bool HasAnyAccess => AvailableStrategies.Count > 0;

    private TierStrategyAccess(
        LlmUserTier tier,
        IReadOnlyList<RagStrategy> availableStrategies,
        bool canAccessCustom)
    {
        Tier = tier;
        AvailableStrategies = availableStrategies;
        CanAccessCustom = canAccessCustom;
    }

    /// <summary>
    /// Creates a TierStrategyAccess for a user tier with the specified strategies.
    /// </summary>
    public static TierStrategyAccess Create(
        LlmUserTier tier,
        IEnumerable<RagStrategy> strategies,
        bool canAccessCustom = false)
    {
        var strategyList = strategies
            .Where(s => s != RagStrategy.None)
            .Distinct()
            .OrderBy(s => (int)s)
            .ToList()
            .AsReadOnly();

        return new TierStrategyAccess(tier, strategyList, canAccessCustom);
    }

    /// <summary>
    /// Creates a TierStrategyAccess with no access (for anonymous users).
    /// </summary>
    public static TierStrategyAccess NoAccess(LlmUserTier tier) =>
        new(tier, Array.Empty<RagStrategy>(), canAccessCustom: false);

    /// <summary>
    /// Checks if the specified strategy is available for this tier.
    /// </summary>
    public bool HasAccessTo(RagStrategy strategy)
    {
        if (strategy == RagStrategy.None)
            return true;

        if (strategy == RagStrategy.Custom)
            return CanAccessCustom;

        return AvailableStrategies.Contains(strategy);
    }

    public override string ToString() =>
        $"TierStrategyAccess[{Tier}: {string.Join(", ", AvailableStrategies.Select(s => s.GetDisplayName()))}]";
}

/// <summary>
/// Result of a tier-strategy access validation.
/// </summary>
/// <param name="IsValid">Whether access is granted.</param>
/// <param name="Tier">The user's tier.</param>
/// <param name="RequestedStrategy">The strategy that was requested.</param>
/// <param name="Message">Validation message.</param>
public record TierStrategyValidationResult(
    bool IsValid,
    LlmUserTier Tier,
    RagStrategy RequestedStrategy,
    string Message)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static TierStrategyValidationResult Success(LlmUserTier tier, RagStrategy strategy) =>
        new(true, tier, strategy, $"Access granted to {strategy.GetDisplayName()} strategy");

    /// <summary>
    /// Creates a failed validation result for insufficient tier.
    /// </summary>
    public static TierStrategyValidationResult AccessDenied(LlmUserTier tier, RagStrategy strategy) =>
        new(false, tier, strategy,
            $"Access denied: {tier} tier cannot access {strategy.GetDisplayName()} strategy");

    /// <summary>
    /// Creates a failed validation result for no access at all.
    /// </summary>
    public static TierStrategyValidationResult NoAccess(LlmUserTier tier) =>
        new(false, tier, RagStrategy.None,
            $"Access denied: {tier} tier has no RAG strategy access");
}
