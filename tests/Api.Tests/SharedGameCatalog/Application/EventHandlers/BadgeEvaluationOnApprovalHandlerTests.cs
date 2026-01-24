using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Application.EventHandlers;

public sealed class BadgeEvaluationOnApprovalHandlerTests
{
    private readonly Mock<IBadgeEvaluator> _badgeEvaluator;
    private readonly Mock<IUserBadgeRepository> _userBadgeRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<MeepleAiDbContext> _dbContext;
    private readonly Mock<ILogger<BadgeEvaluationOnApprovalHandler>> _logger;
    private readonly BadgeEvaluationOnApprovalHandler _sut;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sharedGameId = Guid.NewGuid();

    public BadgeEvaluationOnApprovalHandlerTests()
    {
        _badgeEvaluator = new Mock<IBadgeEvaluator>();
        _userBadgeRepo = new Mock<IUserBadgeRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _dbContext = new Mock<MeepleAiDbContext>();
        _logger = new Mock<ILogger<BadgeEvaluationOnApprovalHandler>>();

        _sut = new BadgeEvaluationOnApprovalHandler(
            _dbContext.Object,
            _badgeEvaluator.Object,
            _userBadgeRepo.Object,
            _shareRequestRepo.Object,
            _unitOfWork.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_WhenNewBadgeEligible_AwardsBadge()
    {
        // Arrange
        var shareRequest = CreateShareRequest();
        var firstContributionBadge = CreateBadge("FIRST_CONTRIBUTION", BadgeTier.Bronze);
        var eligibleBadges = new List<Badge> { firstContributionBadge };
        var existingBadges = new HashSet<Guid>();

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _badgeEvaluator.Setup(e => e.EvaluateEligibleBadgesAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(eligibleBadges);
        _userBadgeRepo.Setup(r => r.GetBadgeIdsByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBadges);

        var domainEvent = new ShareRequestApprovedEvent(_shareRequestId, _adminId, _sharedGameId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _userBadgeRepo.Verify(r => r.AddAsync(
            It.Is<UserBadge>(ub => ub.BadgeId == firstContributionBadge.Id && ub.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBadgeAlreadyOwned_DoesNotAwardAgain()
    {
        // Arrange
        var shareRequest = CreateShareRequest();
        var firstContributionBadge = CreateBadge("FIRST_CONTRIBUTION", BadgeTier.Bronze);
        var eligibleBadges = new List<Badge> { firstContributionBadge };
        var existingBadges = new HashSet<Guid> { firstContributionBadge.Id };

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _badgeEvaluator.Setup(e => e.EvaluateEligibleBadgesAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(eligibleBadges);
        _userBadgeRepo.Setup(r => r.GetBadgeIdsByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBadges);

        var domainEvent = new ShareRequestApprovedEvent(_shareRequestId, _adminId, _sharedGameId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _userBadgeRepo.Verify(r => r.AddAsync(It.IsAny<UserBadge>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoBadgesEligible_DoesNotAwardAnything()
    {
        // Arrange
        var shareRequest = CreateShareRequest();
        var eligibleBadges = new List<Badge>();
        var existingBadges = new HashSet<Guid>();

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _badgeEvaluator.Setup(e => e.EvaluateEligibleBadgesAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(eligibleBadges);
        _userBadgeRepo.Setup(r => r.GetBadgeIdsByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBadges);

        var domainEvent = new ShareRequestApprovedEvent(_shareRequestId, _adminId, _sharedGameId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _userBadgeRepo.Verify(r => r.AddAsync(It.IsAny<UserBadge>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEvaluationThrows_LogsErrorAndContinues()
    {
        // Arrange
        var shareRequest = CreateShareRequest();

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _badgeEvaluator.Setup(e => e.EvaluateEligibleBadgesAsync(_userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var domainEvent = new ShareRequestApprovedEvent(_shareRequestId, _adminId, _sharedGameId);

        // Act
        var act = async () => await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert - Should not throw (graceful failure)
        await act.Should().NotThrowAsync();
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
        return Badge.Create(
            code,
            $"{code} Badge",
            "Test badge description",
            tier,
            BadgeCategory.Contribution,
            requirement,
            null,
            1);
    }
}
