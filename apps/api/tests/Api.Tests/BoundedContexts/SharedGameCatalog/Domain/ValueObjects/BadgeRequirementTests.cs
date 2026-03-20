using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Unit tests for BadgeRequirement value object.
/// </summary>
public class BadgeRequirementTests
{
    #region ForContributionCount Tests

    [Fact]
    public void ForContributionCount_WithValidValue_CreatesBadgeRequirement()
    {
        // Arrange
        var minContributions = 5;

        // Act
        var requirement = BadgeRequirement.ForContributionCount(minContributions);

        // Assert
        requirement.Should().NotBeNull();
        requirement.Type.Should().Be(BadgeRequirementType.ContributionCount);
        requirement.MinContributions.Should().Be(minContributions);
        requirement.MinDocuments.Should().BeNull();
        requirement.MinApprovalRate.Should().BeNull();
        requirement.ConsecutiveApprovalsWithoutChanges.Should().BeNull();
        requirement.TopContributorRank.Should().BeNull();
        requirement.CustomRule.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void ForContributionCount_WithVariousValidValues_CreatesBadgeRequirement(int minContributions)
    {
        // Act
        var requirement = BadgeRequirement.ForContributionCount(minContributions);

        // Assert
        requirement.MinContributions.Should().Be(minContributions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void ForContributionCount_WithInvalidValue_ThrowsArgumentException(int invalidValue)
    {
        // Act
        var act = () => BadgeRequirement.ForContributionCount(invalidValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("minContributions")
            .WithMessage("*Minimum contributions must be at least 1*");
    }

    #endregion

    #region ForDocumentCount Tests

    [Fact]
    public void ForDocumentCount_WithValidValue_CreatesBadgeRequirement()
    {
        // Arrange
        var minDocuments = 10;

        // Act
        var requirement = BadgeRequirement.ForDocumentCount(minDocuments);

        // Assert
        requirement.Should().NotBeNull();
        requirement.Type.Should().Be(BadgeRequirementType.DocumentCount);
        requirement.MinDocuments.Should().Be(minDocuments);
        requirement.MinContributions.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ForDocumentCount_WithInvalidValue_ThrowsArgumentException(int invalidValue)
    {
        // Act
        var act = () => BadgeRequirement.ForDocumentCount(invalidValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("minDocuments")
            .WithMessage("*Minimum documents must be at least 1*");
    }

    #endregion

    #region ForQualityStreak Tests

    [Fact]
    public void ForQualityStreak_WithValidValue_CreatesBadgeRequirement()
    {
        // Arrange
        var consecutiveApprovals = 5;

        // Act
        var requirement = BadgeRequirement.ForQualityStreak(consecutiveApprovals);

        // Assert
        requirement.Should().NotBeNull();
        requirement.Type.Should().Be(BadgeRequirementType.QualityStreak);
        requirement.ConsecutiveApprovalsWithoutChanges.Should().Be(consecutiveApprovals);
        requirement.MinContributions.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ForQualityStreak_WithInvalidValue_ThrowsArgumentException(int invalidValue)
    {
        // Act
        var act = () => BadgeRequirement.ForQualityStreak(invalidValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("consecutiveApprovals")
            .WithMessage("*Consecutive approvals must be at least 1*");
    }

    #endregion

    #region ForTopContributor Tests

    [Fact]
    public void ForTopContributor_WithValidValue_CreatesBadgeRequirement()
    {
        // Arrange
        var topRank = 10;

        // Act
        var requirement = BadgeRequirement.ForTopContributor(topRank);

        // Assert
        requirement.Should().NotBeNull();
        requirement.Type.Should().Be(BadgeRequirementType.TopContributor);
        requirement.TopContributorRank.Should().Be(topRank);
        requirement.MinContributions.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ForTopContributor_WithInvalidValue_ThrowsArgumentException(int invalidValue)
    {
        // Act
        var act = () => BadgeRequirement.ForTopContributor(invalidValue);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("topRank")
            .WithMessage("*Top contributor rank must be at least 1*");
    }

    #endregion

    #region ForFirstContribution Tests

    [Fact]
    public void ForFirstContribution_CreatesBadgeRequirement()
    {
        // Act
        var requirement = BadgeRequirement.ForFirstContribution();

        // Assert
        requirement.Should().NotBeNull();
        requirement.Type.Should().Be(BadgeRequirementType.FirstContribution);
        requirement.MinContributions.Should().BeNull();
        requirement.MinDocuments.Should().BeNull();
        requirement.MinApprovalRate.Should().BeNull();
        requirement.ConsecutiveApprovalsWithoutChanges.Should().BeNull();
        requirement.TopContributorRank.Should().BeNull();
        requirement.CustomRule.Should().BeNull();
    }

    #endregion

    #region ForCustom Tests

    [Fact]
    public void ForCustom_WithValidRule_CreatesBadgeRequirement()
    {
        // Arrange
        var customRule = "special_event_participation";

        // Act
        var requirement = BadgeRequirement.ForCustom(customRule);

        // Assert
        requirement.Should().NotBeNull();
        requirement.Type.Should().Be(BadgeRequirementType.Custom);
        requirement.CustomRule.Should().Be(customRule);
        requirement.MinContributions.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForCustom_WithInvalidRule_ThrowsArgumentException(string? invalidRule)
    {
        // Act
        var act = () => BadgeRequirement.ForCustom(invalidRule!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("customRule")
            .WithMessage("*Custom rule is required*");
    }

    #endregion

    #region GetDescription Tests

    [Fact]
    public void GetDescription_ForContributionCount_ReturnsCorrectDescription()
    {
        // Arrange
        var requirement = BadgeRequirement.ForContributionCount(5);

        // Act
        var description = requirement.GetDescription();

        // Assert
        description.Should().Be("At least 5 approved contributions");
    }

    [Fact]
    public void GetDescription_ForDocumentCount_ReturnsCorrectDescription()
    {
        // Arrange
        var requirement = BadgeRequirement.ForDocumentCount(10);

        // Act
        var description = requirement.GetDescription();

        // Assert
        description.Should().Be("At least 10 documents contributed");
    }

    [Fact]
    public void GetDescription_ForQualityStreak_ReturnsCorrectDescription()
    {
        // Arrange
        var requirement = BadgeRequirement.ForQualityStreak(5);

        // Act
        var description = requirement.GetDescription();

        // Assert
        description.Should().Be("5 consecutive approvals without changes requested");
    }

    [Fact]
    public void GetDescription_ForTopContributor_ReturnsCorrectDescription()
    {
        // Arrange
        var requirement = BadgeRequirement.ForTopContributor(10);

        // Act
        var description = requirement.GetDescription();

        // Assert
        description.Should().Be("Ranked in top 10 contributors");
    }

    [Fact]
    public void GetDescription_ForFirstContribution_ReturnsCorrectDescription()
    {
        // Arrange
        var requirement = BadgeRequirement.ForFirstContribution();

        // Act
        var description = requirement.GetDescription();

        // Assert
        description.Should().Be("Make your first contribution");
    }

    [Fact]
    public void GetDescription_ForCustom_ReturnsCustomRule()
    {
        // Arrange
        var customRule = "special_event_participation";
        var requirement = BadgeRequirement.ForCustom(customRule);

        // Act
        var description = requirement.GetDescription();

        // Assert
        description.Should().Be(customRule);
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange & Act
        var requirement = new BadgeRequirement(
            BadgeRequirementType.ContributionCount,
            minContributions: 5,
            minDocuments: 10,
            minApprovalRate: 0.9m,
            consecutiveApprovalsWithoutChanges: 3,
            topContributorRank: 10,
            customRule: "test_rule");

        // Assert
        requirement.Type.Should().Be(BadgeRequirementType.ContributionCount);
        requirement.MinContributions.Should().Be(5);
        requirement.MinDocuments.Should().Be(10);
        requirement.MinApprovalRate.Should().Be(0.9m);
        requirement.ConsecutiveApprovalsWithoutChanges.Should().Be(3);
        requirement.TopContributorRank.Should().Be(10);
        requirement.CustomRule.Should().Be("test_rule");
    }

    #endregion
}