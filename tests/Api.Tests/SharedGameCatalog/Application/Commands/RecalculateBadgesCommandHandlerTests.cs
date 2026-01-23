using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Application.Commands;

public sealed class RecalculateBadgesCommandHandlerTests
{
    private readonly Mock<IBadgeEvaluator> _badgeEvaluator;
    private readonly Mock<IUserBadgeRepository> _userBadgeRepo;
    private readonly Mock<IBadgeRepository> _badgeRepo;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<ILogger<RecalculateBadgesCommandHandler>> _logger;
    private readonly RecalculateBadgesCommandHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _badgeId1 = Guid.NewGuid();
    private readonly Guid _badgeId2 = Guid.NewGuid();

    public RecalculateBadgesCommandHandlerTests()
    {
        _badgeEvaluator = new Mock<IBadgeEvaluator>();
        _userBadgeRepo = new Mock<IUserBadgeRepository>();
        _badgeRepo = new Mock<IBadgeRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _logger = new Mock<ILogger<RecalculateBadgesCommandHandler>>();

        _sut = new RecalculateBadgesCommandHandler(
            _badgeEvaluator.Object,
            _userBadgeRepo.Object,
            _badgeRepo.Object,
            _unitOfWork.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_WithSingleUser_AwardsMissingBadge()
    {
        // Arrange
        var badge1 = CreateBadge("FIRST_CONTRIBUTION", BadgeTier.Bronze);
        var badge2 = CreateBadge("CONTRIBUTOR_5", BadgeTier.Silver);
        var eligibleBadges = new List<Badge> { badge1, badge2 };
        var currentBadges = new List<UserBadge> { CreateUserBadge(_userId, _badgeId1, "FIRST_CONTRIBUTION") };

        _badgeRepo.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge> { badge1, badge2 });
        _badgeEvaluator.Setup(e => e.EvaluateEligibleBadgesAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(eligibleBadges);
        _userBadgeRepo.Setup(r => r.GetByUserIdAsync(_userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentBadges);

        var command = new RecalculateBadgesCommand { UserId = _userId };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.UsersProcessed.Should().Be(1);
        result.BadgesAwarded.Should().Be(1); // badge2 awarded
        result.BadgesRevoked.Should().Be(0);
        _userBadgeRepo.Verify(r => r.AddAsync(
            It.Is<UserBadge>(ub => ub.BadgeId == badge2.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithSingleUser_RevokesInvalidBadge()
    {
        // Arrange
        var badge1 = CreateBadge("FIRST_CONTRIBUTION", BadgeTier.Bronze);
        var eligibleBadges = new List<Badge> { badge1 };
        var invalidBadge = CreateUserBadge(_userId, _badgeId2, "CONTRIBUTOR_5");
        var currentBadges = new List<UserBadge> { CreateUserBadge(_userId, _badgeId1, "FIRST_CONTRIBUTION"), invalidBadge };

        _badgeRepo.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge> { badge1, CreateBadge("CONTRIBUTOR_5", BadgeTier.Silver) });
        _badgeEvaluator.Setup(e => e.EvaluateEligibleBadgesAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(eligibleBadges);
        _userBadgeRepo.Setup(r => r.GetByUserIdAsync(_userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentBadges);

        var command = new RecalculateBadgesCommand { UserId = _userId };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.UsersProcessed.Should().Be(1);
        result.BadgesAwarded.Should().Be(0);
        result.BadgesRevoked.Should().Be(1); // CONTRIBUTOR_5 revoked
    }

    [Fact]
    public async Task Handle_WithAllUsers_ProcessesZeroUsers()
    {
        // Arrange - GetAllDistinctUserIdsAsync returns empty list (stub implementation)
        _badgeRepo.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Badge>());

        var command = new RecalculateBadgesCommand { UserId = null };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.UsersProcessed.Should().Be(0);
        result.BadgesAwarded.Should().Be(0);
        result.BadgesRevoked.Should().Be(0);
    }

    private ShareRequest CreateShareRequest()
    {
        return ShareRequest.Create(
            _userId,
            Guid.NewGuid(),
            ContributionType.NewGame,
            "Test notes");
    }

    private static Badge CreateBadge(string code, BadgeTier tier)
    {
        var requirement = BadgeRequirement.ForFirstContribution();
        return Badge.Create(code, $"{code} Badge", "Test description", tier, BadgeCategory.Contribution, requirement, null, 1);
    }

    private static UserBadge CreateUserBadge(Guid userId, Guid badgeId, string badgeCode)
    {
        return UserBadge.Award(userId, badgeId, badgeCode, null);
    }
}
