using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class RecalculateBadgesCommandHandlerTests
{
    private readonly Mock<IBadgeEvaluator> _badgeEvaluatorMock;
    private readonly Mock<IUserBadgeRepository> _userBadgeRepositoryMock;
    private readonly Mock<IBadgeRepository> _badgeRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<RecalculateBadgesCommandHandler>> _loggerMock;
    private readonly RecalculateBadgesCommandHandler _handler;

    public RecalculateBadgesCommandHandlerTests()
    {
        _badgeEvaluatorMock = new Mock<IBadgeEvaluator>();
        _userBadgeRepositoryMock = new Mock<IUserBadgeRepository>();
        _badgeRepositoryMock = new Mock<IBadgeRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<RecalculateBadgesCommandHandler>>();
        _handler = new RecalculateBadgesCommandHandler(
            _badgeEvaluatorMock.Object,
            _userBadgeRepositoryMock.Object,
            _badgeRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_SingleUser_AwardsEligibleBadges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RecalculateBadgesCommand { UserId = userId };

        var firstContributionBadge = CreateBadge("FIRST_CONTRIBUTION", BadgeCategory.Contribution);
        var contributor10Badge = CreateBadge("CONTRIBUTOR_10", BadgeCategory.Contribution);

        _badgeRepositoryMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge> { firstContributionBadge, contributor10Badge });

        _badgeEvaluatorMock
            .Setup(e => e.EvaluateEligibleBadgesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge> { firstContributionBadge, contributor10Badge });

        _userBadgeRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserBadge>());

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, response.UsersProcessed);
        Assert.Equal(2, response.BadgesAwarded);
        Assert.Equal(0, response.BadgesRevoked);

        _userBadgeRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<UserBadge>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AllUsers_ProcessesAllUserIds()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();
        var command = new RecalculateBadgesCommand { UserId = null }; // Null = all users

        _badgeRepositoryMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge>());

        _userBadgeRepositoryMock
            .Setup(r => r.GetAllDistinctUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { user1, user2, user3 });

        _badgeEvaluatorMock
            .Setup(e => e.EvaluateEligibleBadgesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge>());

        _userBadgeRepositoryMock
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserBadge>());

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, response.UsersProcessed);

        _badgeEvaluatorMock.Verify(
            e => e.EvaluateEligibleBadgesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_RevokesBadge_WhenUserNoLongerQualifies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RecalculateBadgesCommand { UserId = userId };

        var qualityStreakBadge = CreateBadge("QUALITY_STREAK", BadgeCategory.Quality);
        qualityStreakBadge = Badge.Create(
            code: "QUALITY_STREAK",
            name: "Quality Streak",
            description: "Quality streak badge (revocable)",
            tier: BadgeTier.Silver,
            category: BadgeCategory.Quality,
            requirement: BadgeRequirement.ForQualityStreak(5));

        var existingUserBadge = UserBadge.Award(userId, qualityStreakBadge.Id, qualityStreakBadge.Code, null);

        _badgeRepositoryMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge> { qualityStreakBadge });

        // User no longer qualifies (eligible badges list is empty)
        _badgeEvaluatorMock
            .Setup(e => e.EvaluateEligibleBadgesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge>());

        _userBadgeRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserBadge> { existingUserBadge });

        _badgeRepositoryMock
            .Setup(r => r.GetByIdAsync(qualityStreakBadge.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(qualityStreakBadge);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, response.UsersProcessed);
        Assert.Equal(0, response.BadgesAwarded);
        Assert.Equal(1, response.BadgesRevoked);
        Assert.NotNull(existingUserBadge.RevokedAt);
        Assert.Equal("No longer meets badge requirements", existingUserBadge.RevocationReason);
    }

    [Fact]
    public async Task Handle_DoesNotRevokePermanentBadges_WithContributionCountRequirement()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RecalculateBadgesCommand { UserId = userId };

        // Create contribution milestone badge (permanent - never revoked even if count drops)
        var permanentBadge = Badge.Create(
            code: "CONTRIBUTOR_100",
            name: "100 Contributions Milestone",
            description: "Permanent achievement",
            tier: BadgeTier.Diamond,
            category: BadgeCategory.Contribution,
            requirement: BadgeRequirement.ForContributionCount(100));

        var existingUserBadge = UserBadge.Award(userId, permanentBadge.Id, permanentBadge.Code, null);

        _badgeRepositoryMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge> { permanentBadge });

        // User no longer qualifies (dropped below 100 contributions)
        // But badge should NOT be revoked (permanent milestone)
        _badgeEvaluatorMock
            .Setup(e => e.EvaluateEligibleBadgesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge>());

        _userBadgeRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserBadge> { existingUserBadge });

        _badgeRepositoryMock
            .Setup(r => r.GetByIdAsync(permanentBadge.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permanentBadge);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, response.UsersProcessed);
        Assert.Equal(0, response.BadgesAwarded);
        Assert.Equal(0, response.BadgesRevoked); // Permanent badge (ContributionCount) NOT revoked
        Assert.Null(existingUserBadge.RevokedAt); // Badge still active
    }

    [Fact]
    public async Task Handle_WithEmptyUserList_ProcessesZeroUsers()
    {
        // Arrange
        var command = new RecalculateBadgesCommand { UserId = null }; // All users

        _badgeRepositoryMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge>());

        _userBadgeRepositoryMock
            .Setup(r => r.GetAllDistinctUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>()); // No users in system

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, response.UsersProcessed);
        Assert.Equal(0, response.BadgesAwarded);
        Assert.Equal(0, response.BadgesRevoked);
    }

    #region Helper Methods

    private static Badge CreateBadge(string code, BadgeCategory category)
    {
        return Badge.Create(
            code: code,
            name: $"{code} Badge",
            description: "Test badge",
            tier: BadgeTier.Bronze,
            category: category,
            requirement: BadgeRequirement.ForContributionCount(10));
    }

    #endregion
}
