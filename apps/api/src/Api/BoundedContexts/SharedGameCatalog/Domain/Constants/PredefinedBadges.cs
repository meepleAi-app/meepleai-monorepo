using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Constants;

/// <summary>
/// Predefined badges for the gamification system.
/// These badges are seeded into the database and automatically evaluated.
/// </summary>
public static class PredefinedBadges
{
    #region Contribution Milestone Badges

    /// <summary>
    /// Badge for making the first contribution to the community.
    /// </summary>
    public static Badge FirstContribution => Badge.Create(
        "FIRST_CONTRIBUTION",
        "First Steps",
        "Made your first contribution to the community",
        BadgeTier.Bronze,
        BadgeCategory.Contribution,
        BadgeRequirement.ForFirstContribution(),
        iconUrl: null,
        displayOrder: 10);

    /// <summary>
    /// Badge for contributing 5 games to the community.
    /// </summary>
    public static Badge Contributor5 => Badge.Create(
        "CONTRIBUTOR_5",
        "Helping Hand",
        "Contributed 5 games to the community",
        BadgeTier.Bronze,
        BadgeCategory.Contribution,
        BadgeRequirement.ForContributionCount(5),
        iconUrl: null,
        displayOrder: 20);

    /// <summary>
    /// Badge for contributing 10 games to the community.
    /// </summary>
    public static Badge Contributor10 => Badge.Create(
        "CONTRIBUTOR_10",
        "Community Builder",
        "Contributed 10 games to the community",
        BadgeTier.Silver,
        BadgeCategory.Contribution,
        BadgeRequirement.ForContributionCount(10),
        iconUrl: null,
        displayOrder: 30);

    /// <summary>
    /// Badge for contributing 25 games to the community.
    /// </summary>
    public static Badge Contributor25 => Badge.Create(
        "CONTRIBUTOR_25",
        "Dedicated Contributor",
        "Contributed 25 games to the community",
        BadgeTier.Gold,
        BadgeCategory.Contribution,
        BadgeRequirement.ForContributionCount(25),
        iconUrl: null,
        displayOrder: 40);

    /// <summary>
    /// Badge for contributing 50 games to the community.
    /// </summary>
    public static Badge Contributor50 => Badge.Create(
        "CONTRIBUTOR_50",
        "Community Champion",
        "Contributed 50 games to the community",
        BadgeTier.Platinum,
        BadgeCategory.Contribution,
        BadgeRequirement.ForContributionCount(50),
        iconUrl: null,
        displayOrder: 50);

    /// <summary>
    /// Badge for contributing 100 games to the community.
    /// </summary>
    public static Badge Contributor100 => Badge.Create(
        "CONTRIBUTOR_100",
        "Legendary Contributor",
        "Contributed 100 games to the community",
        BadgeTier.Diamond,
        BadgeCategory.Contribution,
        BadgeRequirement.ForContributionCount(100),
        iconUrl: null,
        displayOrder: 60);

    #endregion

    #region Quality Badges

    /// <summary>
    /// Badge for having 5 consecutive approvals without changes requested.
    /// </summary>
    public static Badge QualityContributor => Badge.Create(
        "QUALITY_CONTRIBUTOR",
        "Quality First",
        "5 contributions approved without changes requested",
        BadgeTier.Silver,
        BadgeCategory.Quality,
        BadgeRequirement.ForQualityStreak(5),
        iconUrl: null,
        displayOrder: 100);

    /// <summary>
    /// Badge for having 10 consecutive approvals without changes requested.
    /// </summary>
    public static Badge QualityMaster => Badge.Create(
        "QUALITY_MASTER",
        "Quality Master",
        "10 contributions approved without changes requested",
        BadgeTier.Gold,
        BadgeCategory.Quality,
        BadgeRequirement.ForQualityStreak(10),
        iconUrl: null,
        displayOrder: 110);

    /// <summary>
    /// Badge for having 25 consecutive approvals without changes requested.
    /// </summary>
    public static Badge PerfectRecord => Badge.Create(
        "PERFECT_RECORD",
        "Perfect Record",
        "25 contributions approved without changes requested",
        BadgeTier.Platinum,
        BadgeCategory.Quality,
        BadgeRequirement.ForQualityStreak(25),
        iconUrl: null,
        displayOrder: 120);

    #endregion

    #region Document Badges

    /// <summary>
    /// Badge for contributing 10 documents across submissions.
    /// </summary>
    public static Badge DocumentContributor => Badge.Create(
        "DOCUMENT_CONTRIBUTOR",
        "Document Helper",
        "Contributed 10 documents across submissions",
        BadgeTier.Silver,
        BadgeCategory.Contribution,
        BadgeRequirement.ForDocumentCount(10),
        iconUrl: null,
        displayOrder: 200);

    /// <summary>
    /// Badge for contributing 25 documents across submissions.
    /// </summary>
    public static Badge DocumentMaster => Badge.Create(
        "DOCUMENT_MASTER",
        "Document Master",
        "Contributed 25 documents across submissions",
        BadgeTier.Gold,
        BadgeCategory.Contribution,
        BadgeRequirement.ForDocumentCount(25),
        iconUrl: null,
        displayOrder: 210);

    /// <summary>
    /// Badge for contributing 50 documents across submissions.
    /// </summary>
    public static Badge DocumentExpert => Badge.Create(
        "DOCUMENT_EXPERT",
        "Document Expert",
        "Contributed 50 documents across submissions",
        BadgeTier.Platinum,
        BadgeCategory.Contribution,
        BadgeRequirement.ForDocumentCount(50),
        iconUrl: null,
        displayOrder: 220);

    #endregion

    #region Leaderboard Badges

    /// <summary>
    /// Badge for ranking in the top 10 contributors.
    /// </summary>
    public static Badge TopContributor => Badge.Create(
        "TOP_CONTRIBUTOR",
        "Top Contributor",
        "Ranked in top 10 contributors",
        BadgeTier.Gold,
        BadgeCategory.Engagement,
        BadgeRequirement.ForTopContributor(10),
        iconUrl: null,
        displayOrder: 300);

    /// <summary>
    /// Badge for ranking in the top 3 contributors.
    /// </summary>
    public static Badge TopThree => Badge.Create(
        "TOP_THREE",
        "Elite Contributor",
        "Ranked in top 3 contributors",
        BadgeTier.Platinum,
        BadgeCategory.Engagement,
        BadgeRequirement.ForTopContributor(3),
        iconUrl: null,
        displayOrder: 310);

    /// <summary>
    /// Badge for being the #1 contributor.
    /// </summary>
    public static Badge NumberOne => Badge.Create(
        "NUMBER_ONE",
        "Champion",
        "Ranked #1 contributor",
        BadgeTier.Diamond,
        BadgeCategory.Engagement,
        BadgeRequirement.ForTopContributor(1),
        iconUrl: null,
        displayOrder: 320);

    #endregion

    /// <summary>
    /// Gets all predefined badges.
    /// </summary>
    public static IReadOnlyList<Badge> All => new[]
    {
        // Contribution milestones
        FirstContribution,
        Contributor5,
        Contributor10,
        Contributor25,
        Contributor50,
        Contributor100,

        // Quality badges
        QualityContributor,
        QualityMaster,
        PerfectRecord,

        // Document badges
        DocumentContributor,
        DocumentMaster,
        DocumentExpert,

        // Leaderboard badges
        TopContributor,
        TopThree,
        NumberOne
    };

    /// <summary>
    /// Gets all badge codes.
    /// </summary>
    public static IReadOnlyList<string> GetAllCodes() => All.Select(b => b.Code).ToList();

    /// <summary>
    /// Gets badges by category.
    /// </summary>
    public static IReadOnlyList<Badge> GetByCategory(BadgeCategory category)
        => All.Where(b => b.Category == category).ToList();

    /// <summary>
    /// Gets badges by tier.
    /// </summary>
    public static IReadOnlyList<Badge> GetByTier(BadgeTier tier)
        => All.Where(b => b.Tier == tier).ToList();
}
