using Api.BoundedContexts.SharedGameCatalog.Domain.Constants;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Constants;

/// <summary>
/// Unit tests for PredefinedBadges constants.
/// </summary>
public class PredefinedBadgesTests
{
    #region Contribution Milestone Badges Tests

    [Fact]
    public void FirstContribution_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.FirstContribution;

        // Assert
        badge.Code.Should().Be("FIRST_CONTRIBUTION");
        badge.Name.Should().Be("First Steps");
        badge.Tier.Should().Be(BadgeTier.Bronze);
        badge.Category.Should().Be(BadgeCategory.Contribution);
        badge.Requirement.Type.Should().Be(BadgeRequirementType.FirstContribution);
        badge.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Contributor5_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.Contributor5;

        // Assert
        badge.Code.Should().Be("CONTRIBUTOR_5");
        badge.Name.Should().Be("Helping Hand");
        badge.Tier.Should().Be(BadgeTier.Bronze);
        badge.Requirement.Type.Should().Be(BadgeRequirementType.ContributionCount);
        badge.Requirement.MinContributions.Should().Be(5);
    }

    [Fact]
    public void Contributor10_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.Contributor10;

        // Assert
        badge.Code.Should().Be("CONTRIBUTOR_10");
        badge.Tier.Should().Be(BadgeTier.Silver);
        badge.Requirement.MinContributions.Should().Be(10);
    }

    [Fact]
    public void Contributor25_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.Contributor25;

        // Assert
        badge.Code.Should().Be("CONTRIBUTOR_25");
        badge.Tier.Should().Be(BadgeTier.Gold);
        badge.Requirement.MinContributions.Should().Be(25);
    }

    [Fact]
    public void Contributor50_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.Contributor50;

        // Assert
        badge.Code.Should().Be("CONTRIBUTOR_50");
        badge.Tier.Should().Be(BadgeTier.Platinum);
        badge.Requirement.MinContributions.Should().Be(50);
    }

    [Fact]
    public void Contributor100_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.Contributor100;

        // Assert
        badge.Code.Should().Be("CONTRIBUTOR_100");
        badge.Name.Should().Be("Legendary Contributor");
        badge.Tier.Should().Be(BadgeTier.Diamond);
        badge.Requirement.MinContributions.Should().Be(100);
    }

    #endregion

    #region Quality Badges Tests

    [Fact]
    public void QualityContributor_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.QualityContributor;

        // Assert
        badge.Code.Should().Be("QUALITY_CONTRIBUTOR");
        badge.Name.Should().Be("Quality First");
        badge.Tier.Should().Be(BadgeTier.Silver);
        badge.Category.Should().Be(BadgeCategory.Quality);
        badge.Requirement.Type.Should().Be(BadgeRequirementType.QualityStreak);
        badge.Requirement.ConsecutiveApprovalsWithoutChanges.Should().Be(5);
    }

    [Fact]
    public void QualityMaster_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.QualityMaster;

        // Assert
        badge.Code.Should().Be("QUALITY_MASTER");
        badge.Tier.Should().Be(BadgeTier.Gold);
        badge.Requirement.ConsecutiveApprovalsWithoutChanges.Should().Be(10);
    }

    [Fact]
    public void PerfectRecord_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.PerfectRecord;

        // Assert
        badge.Code.Should().Be("PERFECT_RECORD");
        badge.Tier.Should().Be(BadgeTier.Platinum);
        badge.Requirement.ConsecutiveApprovalsWithoutChanges.Should().Be(25);
    }

    #endregion

    #region Document Badges Tests

    [Fact]
    public void DocumentContributor_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.DocumentContributor;

        // Assert
        badge.Code.Should().Be("DOCUMENT_CONTRIBUTOR");
        badge.Tier.Should().Be(BadgeTier.Silver);
        badge.Requirement.Type.Should().Be(BadgeRequirementType.DocumentCount);
        badge.Requirement.MinDocuments.Should().Be(10);
    }

    [Fact]
    public void DocumentMaster_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.DocumentMaster;

        // Assert
        badge.Code.Should().Be("DOCUMENT_MASTER");
        badge.Tier.Should().Be(BadgeTier.Gold);
        badge.Requirement.MinDocuments.Should().Be(25);
    }

    [Fact]
    public void DocumentExpert_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.DocumentExpert;

        // Assert
        badge.Code.Should().Be("DOCUMENT_EXPERT");
        badge.Tier.Should().Be(BadgeTier.Platinum);
        badge.Requirement.MinDocuments.Should().Be(50);
    }

    #endregion

    #region Leaderboard Badges Tests

    [Fact]
    public void TopContributor_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.TopContributor;

        // Assert
        badge.Code.Should().Be("TOP_CONTRIBUTOR");
        badge.Tier.Should().Be(BadgeTier.Gold);
        badge.Category.Should().Be(BadgeCategory.Engagement);
        badge.Requirement.Type.Should().Be(BadgeRequirementType.TopContributor);
        badge.Requirement.TopContributorRank.Should().Be(10);
    }

    [Fact]
    public void TopThree_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.TopThree;

        // Assert
        badge.Code.Should().Be("TOP_THREE");
        badge.Tier.Should().Be(BadgeTier.Platinum);
        badge.Requirement.TopContributorRank.Should().Be(3);
    }

    [Fact]
    public void NumberOne_HasCorrectProperties()
    {
        // Act
        var badge = PredefinedBadges.NumberOne;

        // Assert
        badge.Code.Should().Be("NUMBER_ONE");
        badge.Name.Should().Be("Champion");
        badge.Tier.Should().Be(BadgeTier.Diamond);
        badge.Requirement.TopContributorRank.Should().Be(1);
    }

    #endregion

    #region All Badges Collection Tests

    [Fact]
    public void All_ContainsExpectedNumberOfBadges()
    {
        // Act
        var allBadges = PredefinedBadges.All;

        // Assert
        allBadges.Should().HaveCount(15);
    }

    [Fact]
    public void All_ContainsAllContributionMilestones()
    {
        // Act
        var allBadges = PredefinedBadges.All;

        // Assert
        allBadges.Should().Contain(b => b.Code == "FIRST_CONTRIBUTION");
        allBadges.Should().Contain(b => b.Code == "CONTRIBUTOR_5");
        allBadges.Should().Contain(b => b.Code == "CONTRIBUTOR_10");
        allBadges.Should().Contain(b => b.Code == "CONTRIBUTOR_25");
        allBadges.Should().Contain(b => b.Code == "CONTRIBUTOR_50");
        allBadges.Should().Contain(b => b.Code == "CONTRIBUTOR_100");
    }

    [Fact]
    public void All_ContainsAllQualityBadges()
    {
        // Act
        var allBadges = PredefinedBadges.All;

        // Assert
        allBadges.Should().Contain(b => b.Code == "QUALITY_CONTRIBUTOR");
        allBadges.Should().Contain(b => b.Code == "QUALITY_MASTER");
        allBadges.Should().Contain(b => b.Code == "PERFECT_RECORD");
    }

    [Fact]
    public void All_ContainsAllDocumentBadges()
    {
        // Act
        var allBadges = PredefinedBadges.All;

        // Assert
        allBadges.Should().Contain(b => b.Code == "DOCUMENT_CONTRIBUTOR");
        allBadges.Should().Contain(b => b.Code == "DOCUMENT_MASTER");
        allBadges.Should().Contain(b => b.Code == "DOCUMENT_EXPERT");
    }

    [Fact]
    public void All_ContainsAllLeaderboardBadges()
    {
        // Act
        var allBadges = PredefinedBadges.All;

        // Assert
        allBadges.Should().Contain(b => b.Code == "TOP_CONTRIBUTOR");
        allBadges.Should().Contain(b => b.Code == "TOP_THREE");
        allBadges.Should().Contain(b => b.Code == "NUMBER_ONE");
    }

    [Fact]
    public void All_HasUniqueCodes()
    {
        // Act
        var allBadges = PredefinedBadges.All;
        var codes = allBadges.Select(b => b.Code).ToList();

        // Assert
        codes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_AllBadgesAreActive()
    {
        // Act
        var allBadges = PredefinedBadges.All;

        // Assert
        allBadges.Should().OnlyContain(b => b.IsActive);
    }

    #endregion

    #region GetAllCodes Tests

    [Fact]
    public void GetAllCodes_ReturnsAllBadgeCodes()
    {
        // Act
        var allCodes = PredefinedBadges.GetAllCodes();

        // Assert
        allCodes.Should().HaveCount(15);
        allCodes.Should().Contain("FIRST_CONTRIBUTION");
        allCodes.Should().Contain("CONTRIBUTOR_100");
        allCodes.Should().Contain("NUMBER_ONE");
    }

    [Fact]
    public void GetAllCodes_HasUniqueEntries()
    {
        // Act
        var allCodes = PredefinedBadges.GetAllCodes();

        // Assert
        allCodes.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region GetByCategory Tests

    [Fact]
    public void GetByCategory_Contribution_ReturnsCorrectBadges()
    {
        // Act
        var contributionBadges = PredefinedBadges.GetByCategory(BadgeCategory.Contribution);

        // Assert
        contributionBadges.Should().HaveCount(9); // 6 contribution milestones + 3 document badges
        contributionBadges.Should().OnlyContain(b => b.Category == BadgeCategory.Contribution);
    }

    [Fact]
    public void GetByCategory_Quality_ReturnsCorrectBadges()
    {
        // Act
        var qualityBadges = PredefinedBadges.GetByCategory(BadgeCategory.Quality);

        // Assert
        qualityBadges.Should().HaveCount(3);
        qualityBadges.Should().OnlyContain(b => b.Category == BadgeCategory.Quality);
    }

    [Fact]
    public void GetByCategory_Engagement_ReturnsCorrectBadges()
    {
        // Act
        var engagementBadges = PredefinedBadges.GetByCategory(BadgeCategory.Engagement);

        // Assert
        engagementBadges.Should().HaveCount(3);
        engagementBadges.Should().OnlyContain(b => b.Category == BadgeCategory.Engagement);
    }

    [Fact]
    public void GetByCategory_Special_ReturnsEmptyList()
    {
        // Act
        var specialBadges = PredefinedBadges.GetByCategory(BadgeCategory.Special);

        // Assert
        specialBadges.Should().BeEmpty();
    }

    #endregion

    #region GetByTier Tests

    [Fact]
    public void GetByTier_Bronze_ReturnsCorrectBadges()
    {
        // Act
        var bronzeBadges = PredefinedBadges.GetByTier(BadgeTier.Bronze);

        // Assert
        bronzeBadges.Should().HaveCount(2); // FirstContribution, Contributor5
        bronzeBadges.Should().OnlyContain(b => b.Tier == BadgeTier.Bronze);
    }

    [Fact]
    public void GetByTier_Silver_ReturnsCorrectBadges()
    {
        // Act
        var silverBadges = PredefinedBadges.GetByTier(BadgeTier.Silver);

        // Assert
        silverBadges.Should().HaveCount(3); // Contributor10, QualityContributor, DocumentContributor
        silverBadges.Should().OnlyContain(b => b.Tier == BadgeTier.Silver);
    }

    [Fact]
    public void GetByTier_Gold_ReturnsCorrectBadges()
    {
        // Act
        var goldBadges = PredefinedBadges.GetByTier(BadgeTier.Gold);

        // Assert
        goldBadges.Should().HaveCount(4); // Contributor25, QualityMaster, DocumentMaster, TopContributor
        goldBadges.Should().OnlyContain(b => b.Tier == BadgeTier.Gold);
    }

    [Fact]
    public void GetByTier_Platinum_ReturnsCorrectBadges()
    {
        // Act
        var platinumBadges = PredefinedBadges.GetByTier(BadgeTier.Platinum);

        // Assert
        platinumBadges.Should().HaveCount(4); // Contributor50, PerfectRecord, DocumentExpert, TopThree
        platinumBadges.Should().OnlyContain(b => b.Tier == BadgeTier.Platinum);
    }

    [Fact]
    public void GetByTier_Diamond_ReturnsCorrectBadges()
    {
        // Act
        var diamondBadges = PredefinedBadges.GetByTier(BadgeTier.Diamond);

        // Assert
        diamondBadges.Should().HaveCount(2); // Contributor100, NumberOne
        diamondBadges.Should().OnlyContain(b => b.Tier == BadgeTier.Diamond);
    }

    #endregion

    #region DisplayOrder Tests

    [Fact]
    public void AllBadges_HaveIncrementingDisplayOrder()
    {
        // Act
        var allBadges = PredefinedBadges.All;
        var displayOrders = allBadges.Select(b => b.DisplayOrder).ToList();

        // Assert
        displayOrders.Should().OnlyHaveUniqueItems();
        displayOrders.Should().BeInAscendingOrder();
    }

    [Fact]
    public void ContributionMilestones_HaveCorrectDisplayOrderRange()
    {
        // Assert
        PredefinedBadges.FirstContribution.DisplayOrder.Should().Be(10);
        PredefinedBadges.Contributor100.DisplayOrder.Should().Be(60);
    }

    [Fact]
    public void QualityBadges_HaveCorrectDisplayOrderRange()
    {
        // Assert
        PredefinedBadges.QualityContributor.DisplayOrder.Should().Be(100);
        PredefinedBadges.PerfectRecord.DisplayOrder.Should().Be(120);
    }

    [Fact]
    public void DocumentBadges_HaveCorrectDisplayOrderRange()
    {
        // Assert
        PredefinedBadges.DocumentContributor.DisplayOrder.Should().Be(200);
        PredefinedBadges.DocumentExpert.DisplayOrder.Should().Be(220);
    }

    [Fact]
    public void LeaderboardBadges_HaveCorrectDisplayOrderRange()
    {
        // Assert
        PredefinedBadges.TopContributor.DisplayOrder.Should().Be(300);
        PredefinedBadges.NumberOne.DisplayOrder.Should().Be(320);
    }

    #endregion

    #region Tier Progression Tests

    [Fact]
    public void ContributionMilestones_HaveProgressiveTiers()
    {
        // Assert
        PredefinedBadges.FirstContribution.Tier.Should().Be(BadgeTier.Bronze);
        PredefinedBadges.Contributor5.Tier.Should().Be(BadgeTier.Bronze);
        PredefinedBadges.Contributor10.Tier.Should().Be(BadgeTier.Silver);
        PredefinedBadges.Contributor25.Tier.Should().Be(BadgeTier.Gold);
        PredefinedBadges.Contributor50.Tier.Should().Be(BadgeTier.Platinum);
        PredefinedBadges.Contributor100.Tier.Should().Be(BadgeTier.Diamond);
    }

    [Fact]
    public void QualityBadges_HaveProgressiveTiers()
    {
        // Assert
        PredefinedBadges.QualityContributor.Tier.Should().Be(BadgeTier.Silver);
        PredefinedBadges.QualityMaster.Tier.Should().Be(BadgeTier.Gold);
        PredefinedBadges.PerfectRecord.Tier.Should().Be(BadgeTier.Platinum);
    }

    [Fact]
    public void LeaderboardBadges_HaveProgressiveTiers()
    {
        // Assert
        PredefinedBadges.TopContributor.Tier.Should().Be(BadgeTier.Gold);
        PredefinedBadges.TopThree.Tier.Should().Be(BadgeTier.Platinum);
        PredefinedBadges.NumberOne.Tier.Should().Be(BadgeTier.Diamond);
    }

    #endregion
}