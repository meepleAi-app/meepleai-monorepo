using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

[Trait("Category", TestCategories.Unit)]
public class BadgeEvaluatorTests
{
    private readonly Mock<IBadgeRepository> _badgeRepositoryMock;
    private readonly Mock<IShareRequestRepository> _shareRequestRepositoryMock;
    private readonly Mock<ISharedGameDocumentRepository> _sharedGameDocumentRepositoryMock;
    private readonly IBadgeEvaluator _evaluator;

    public BadgeEvaluatorTests()
    {
        _badgeRepositoryMock = new Mock<IBadgeRepository>();
        _shareRequestRepositoryMock = new Mock<IShareRequestRepository>();
        _sharedGameDocumentRepositoryMock = new Mock<ISharedGameDocumentRepository>();
        _evaluator = new BadgeEvaluator(
            _badgeRepositoryMock.Object,
            _shareRequestRepositoryMock.Object,
            _sharedGameDocumentRepositoryMock.Object);
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_FirstContribution_WithOneApproval_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requirement = BadgeRequirement.ForFirstContribution();

        _shareRequestRepositoryMock
            .Setup(r => r.CountApprovedByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _evaluator.CheckBadgeRequirementAsync(
            userId,
            requirement,
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        _shareRequestRepositoryMock.Verify(
            r => r.CountApprovedByUserAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_FirstContribution_WithZeroApprovals_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requirement = BadgeRequirement.ForFirstContribution();

        _shareRequestRepositoryMock
            .Setup(r => r.CountApprovedByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _evaluator.CheckBadgeRequirementAsync(
            userId,
            requirement,
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_FirstContribution_WithMultipleApprovals_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requirement = BadgeRequirement.ForFirstContribution();

        _shareRequestRepositoryMock
            .Setup(r => r.CountApprovedByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _evaluator.CheckBadgeRequirementAsync(
            userId,
            requirement,
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse(); // First contribution is EXACTLY 1, not >= 1
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_ContributionCount_MeetsMinimum_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requirement = BadgeRequirement.ForContributionCount(10);

        _shareRequestRepositoryMock
            .Setup(r => r.CountApprovedByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);

        // Act
        var result = await _evaluator.CheckBadgeRequirementAsync(
            userId,
            requirement,
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_ContributionCount_BelowMinimum_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requirement = BadgeRequirement.ForContributionCount(10);

        _shareRequestRepositoryMock
            .Setup(r => r.CountApprovedByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _evaluator.CheckBadgeRequirementAsync(
            userId,
            requirement,
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_DocumentCount_MeetsMinimum_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requirement = BadgeRequirement.ForDocumentCount(10);

        _sharedGameDocumentRepositoryMock
            .Setup(r => r.CountByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);

        // Act
        var result = await _evaluator.CheckBadgeRequirementAsync(
            userId,
            requirement,
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_DocumentCount_BelowMinimum_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requirement = BadgeRequirement.ForDocumentCount(10);

        _sharedGameDocumentRepositoryMock
            .Setup(r => r.CountByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _evaluator.CheckBadgeRequirementAsync(
            userId,
            requirement,
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    // NOTE: QualityStreak tests require ShareRequest objects which are sealed aggregates.
    // These tests are covered by integration tests with actual database.
    // Unit testing QualityStreak logic would require making ShareRequest mockable or
    // extracting streak validation to a separate service.

    [Fact]
    public async Task CheckBadgeRequirementAsync_TopContributor_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requirement = BadgeRequirement.ForTopContributor(10);

        // Act
        var result = await _evaluator.CheckBadgeRequirementAsync(
            userId,
            requirement,
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse(); // TopContributor is handled by scheduled job, not real-time evaluation
    }

    [Fact]
    public async Task EvaluateEligibleBadgesAsync_ReturnsOnlyEligibleBadges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var firstContributionBadge = CreateBadge("FIRST_CONTRIBUTION", BadgeRequirement.ForFirstContribution());
        var contributor10Badge = CreateBadge("CONTRIBUTOR_10", BadgeRequirement.ForContributionCount(10));

        _badgeRepositoryMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge> { firstContributionBadge, contributor10Badge });

        // User has 1 contribution (eligible for first contribution only)
        _shareRequestRepositoryMock
            .Setup(r => r.CountApprovedByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var eligibleBadges = await _evaluator.EvaluateEligibleBadgesAsync(
            userId,
            TestContext.Current.CancellationToken);

        // Assert
        eligibleBadges.Should().ContainSingle();
        eligibleBadges[0].Code.Should().Be("FIRST_CONTRIBUTION");
    }

    #region Helper Methods

    private static Badge CreateBadge(string code, BadgeRequirement requirement)
    {
        return Badge.Create(
            code: code,
            name: $"{code} Badge",
            description: "Test badge",
            tier: BadgeTier.Bronze,
            category: BadgeCategory.Contribution,
            requirement: requirement);
    }

    #endregion
}