namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing the requirements for earning a badge.
/// Different badge types use different subsets of the requirement properties.
/// </summary>
public sealed class BadgeRequirement
{
    /// <summary>
    /// Gets the type of requirement.
    /// </summary>
    public BadgeRequirementType Type { get; }

    /// <summary>
    /// Gets the minimum number of approved contributions required.
    /// Used for ContributionCount requirement type.
    /// </summary>
    public int? MinContributions { get; }

    /// <summary>
    /// Gets the minimum number of documents contributed required.
    /// Used for DocumentCount requirement type.
    /// </summary>
    public int? MinDocuments { get; }

    /// <summary>
    /// Gets the minimum approval rate required (0.0 to 1.0).
    /// Used for quality-related requirements.
    /// </summary>
    public decimal? MinApprovalRate { get; }

    /// <summary>
    /// Gets the number of consecutive approvals without changes requested.
    /// Used for QualityStreak requirement type.
    /// </summary>
    public int? ConsecutiveApprovalsWithoutChanges { get; }

    /// <summary>
    /// Gets the top contributor rank required.
    /// Used for TopContributor requirement type (e.g., top 10).
    /// </summary>
    public int? TopContributorRank { get; }

    /// <summary>
    /// Gets a custom rule expression for complex requirements.
    /// Used for Custom requirement type.
    /// </summary>
    public string? CustomRule { get; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable S1144 // Unused private types or members should be removed - Required for EF Core
    private BadgeRequirement()
    {
    }
#pragma warning restore S1144

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal BadgeRequirement(
        BadgeRequirementType type,
        int? minContributions,
        int? minDocuments,
        decimal? minApprovalRate,
        int? consecutiveApprovalsWithoutChanges,
        int? topContributorRank,
        string? customRule)
    {
        Type = type;
        MinContributions = minContributions;
        MinDocuments = minDocuments;
        MinApprovalRate = minApprovalRate;
        ConsecutiveApprovalsWithoutChanges = consecutiveApprovalsWithoutChanges;
        TopContributorRank = topContributorRank;
        CustomRule = customRule;
    }

    /// <summary>
    /// Creates a requirement for contribution count.
    /// </summary>
    /// <param name="minContributions">Minimum number of approved contributions required.</param>
    /// <returns>A new BadgeRequirement instance.</returns>
    /// <exception cref="ArgumentException">Thrown when minContributions is less than 1.</exception>
    public static BadgeRequirement ForContributionCount(int minContributions)
    {
        if (minContributions < 1)
            throw new ArgumentException("Minimum contributions must be at least 1", nameof(minContributions));

        return new BadgeRequirement(
            BadgeRequirementType.ContributionCount,
            minContributions,
            minDocuments: null,
            minApprovalRate: null,
            consecutiveApprovalsWithoutChanges: null,
            topContributorRank: null,
            customRule: null);
    }

    /// <summary>
    /// Creates a requirement for document count.
    /// </summary>
    /// <param name="minDocuments">Minimum number of documents contributed required.</param>
    /// <returns>A new BadgeRequirement instance.</returns>
    /// <exception cref="ArgumentException">Thrown when minDocuments is less than 1.</exception>
    public static BadgeRequirement ForDocumentCount(int minDocuments)
    {
        if (minDocuments < 1)
            throw new ArgumentException("Minimum documents must be at least 1", nameof(minDocuments));

        return new BadgeRequirement(
            BadgeRequirementType.DocumentCount,
            minContributions: null,
            minDocuments,
            minApprovalRate: null,
            consecutiveApprovalsWithoutChanges: null,
            topContributorRank: null,
            customRule: null);
    }

    /// <summary>
    /// Creates a requirement for quality streak (consecutive approvals without changes).
    /// </summary>
    /// <param name="consecutiveApprovals">Number of consecutive approvals required.</param>
    /// <returns>A new BadgeRequirement instance.</returns>
    /// <exception cref="ArgumentException">Thrown when consecutiveApprovals is less than 1.</exception>
    public static BadgeRequirement ForQualityStreak(int consecutiveApprovals)
    {
        if (consecutiveApprovals < 1)
            throw new ArgumentException("Consecutive approvals must be at least 1", nameof(consecutiveApprovals));

        return new BadgeRequirement(
            BadgeRequirementType.QualityStreak,
            minContributions: null,
            minDocuments: null,
            minApprovalRate: null,
            consecutiveApprovals,
            topContributorRank: null,
            customRule: null);
    }

    /// <summary>
    /// Creates a requirement for top contributor ranking.
    /// </summary>
    /// <param name="topRank">Top N rank required (e.g., 10 for top 10).</param>
    /// <returns>A new BadgeRequirement instance.</returns>
    /// <exception cref="ArgumentException">Thrown when topRank is less than 1.</exception>
    public static BadgeRequirement ForTopContributor(int topRank)
    {
        if (topRank < 1)
            throw new ArgumentException("Top contributor rank must be at least 1", nameof(topRank));

        return new BadgeRequirement(
            BadgeRequirementType.TopContributor,
            minContributions: null,
            minDocuments: null,
            minApprovalRate: null,
            consecutiveApprovalsWithoutChanges: null,
            topRank,
            customRule: null);
    }

    /// <summary>
    /// Creates a requirement for first contribution.
    /// </summary>
    /// <returns>A new BadgeRequirement instance.</returns>
    public static BadgeRequirement ForFirstContribution()
    {
        return new BadgeRequirement(
            BadgeRequirementType.FirstContribution,
            minContributions: null,
            minDocuments: null,
            minApprovalRate: null,
            consecutiveApprovalsWithoutChanges: null,
            topContributorRank: null,
            customRule: null);
    }

    /// <summary>
    /// Creates a custom requirement with a rule expression.
    /// </summary>
    /// <param name="customRule">The custom rule expression.</param>
    /// <returns>A new BadgeRequirement instance.</returns>
    /// <exception cref="ArgumentException">Thrown when customRule is null or empty.</exception>
    public static BadgeRequirement ForCustom(string customRule)
    {
        if (string.IsNullOrWhiteSpace(customRule))
            throw new ArgumentException("Custom rule is required", nameof(customRule));

        return new BadgeRequirement(
            BadgeRequirementType.Custom,
            minContributions: null,
            minDocuments: null,
            minApprovalRate: null,
            consecutiveApprovalsWithoutChanges: null,
            topContributorRank: null,
            customRule);
    }

    /// <summary>
    /// Gets a human-readable description of this requirement.
    /// </summary>
    public string GetDescription()
    {
        return Type switch
        {
            BadgeRequirementType.ContributionCount => $"At least {MinContributions} approved contributions",
            BadgeRequirementType.DocumentCount => $"At least {MinDocuments} documents contributed",
            BadgeRequirementType.QualityStreak => $"{ConsecutiveApprovalsWithoutChanges} consecutive approvals without changes requested",
            BadgeRequirementType.TopContributor => $"Ranked in top {TopContributorRank} contributors",
            BadgeRequirementType.FirstContribution => "Make your first contribution",
            BadgeRequirementType.Custom => CustomRule ?? "Custom requirement",
            _ => "Unknown requirement"
        };
    }
}
