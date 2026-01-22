using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Infrastructure.Services;

public sealed class BadgeEvaluatorTests
{
    private readonly Mock<IBadgeRepository> _badgeRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<ISharedGameDocumentRepository> _documentRepo;
    private readonly BadgeEvaluator _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public BadgeEvaluatorTests()
    {
        _badgeRepo = new Mock<IBadgeRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _documentRepo = new Mock<ISharedGameDocumentRepository>();

        _sut = new BadgeEvaluator(
            _badgeRepo.Object,
            _shareRequestRepo.Object,
            _documentRepo.Object);
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_FirstContribution_WhenCountIsOne_ReturnsTrue()
    {
        // Arrange
        var requirement = BadgeRequirement.FirstContribution();
        _shareRequestRepo.Setup(r => r.CountApprovedByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.CheckBadgeRequirementAsync(_userId, requirement, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_FirstContribution_WhenCountIsGreaterThanOne_ReturnsFalse()
    {
        // Arrange
        var requirement = BadgeRequirement.FirstContribution();
        _shareRequestRepo.Setup(r => r.CountApprovedByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _sut.CheckBadgeRequirementAsync(_userId, requirement, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_ContributionCount_WhenMeetsMinimum_ReturnsTrue()
    {
        // Arrange
        var requirement = BadgeRequirement.ContributionCount(10);
        _shareRequestRepo.Setup(r => r.CountApprovedByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);

        // Act
        var result = await _sut.CheckBadgeRequirementAsync(_userId, requirement, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_DocumentCount_WhenMeetsMinimum_ReturnsTrue()
    {
        // Arrange
        var requirement = BadgeRequirement.DocumentCount(10);
        _documentRepo.Setup(r => r.CountByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);

        // Act
        var result = await _sut.CheckBadgeRequirementAsync(_userId, requirement, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckBadgeRequirementAsync_DocumentCount_WhenBelowMinimum_ReturnsFalse()
    {
        // Arrange
        var requirement = BadgeRequirement.DocumentCount(10);
        _documentRepo.Setup(r => r.CountByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _sut.CheckBadgeRequirementAsync(_userId, requirement, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateEligibleBadgesAsync_ReturnsOnlyEligibleBadges()
    {
        // Arrange
        var badge1 = CreateBadge("FIRST_CONTRIBUTION", BadgeRequirement.FirstContribution());
        var badge2 = CreateBadge("CONTRIBUTOR_10", BadgeRequirement.ContributionCount(10));
        var allBadges = new List<Badge> { badge1, badge2 };

        _badgeRepo.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allBadges);
        _shareRequestRepo.Setup(r => r.CountApprovedByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // Only meets FirstContribution

        // Act
        var result = await _sut.EvaluateEligibleBadgesAsync(_userId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Code.Should().Be("FIRST_CONTRIBUTION");
    }

    private static Badge CreateBadge(string code, BadgeRequirement requirement)
    {
        return Badge.Create(
            code,
            $"{code} Badge",
            "Test description",
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            null,
            1,
            requirement);
    }
}
