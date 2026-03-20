using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

using UserBadgeDto = Api.BoundedContexts.SharedGameCatalog.Application.DTOs.UserBadgeDto;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

[Trait("Category", TestCategories.Unit)]
public class TopContributorBadgeJobTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IBadgeRepository> _badgeRepositoryMock;
    private readonly Mock<IUserBadgeRepository> _userBadgeRepositoryMock;
    private readonly Mock<ILogger<TopContributorBadgeJob>> _loggerMock;
    private readonly Mock<IJobExecutionContext> _jobContextMock;
    private readonly TopContributorBadgeJob _job;

    public TopContributorBadgeJobTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _badgeRepositoryMock = new Mock<IBadgeRepository>();
        _userBadgeRepositoryMock = new Mock<IUserBadgeRepository>();
        _loggerMock = new Mock<ILogger<TopContributorBadgeJob>>();
        _jobContextMock = new Mock<IJobExecutionContext>();
        _jobContextMock.Setup(c => c.CancellationToken).Returns(TestContext.Current.CancellationToken);
        _job = new TopContributorBadgeJob(
            _mediatorMock.Object,
            _badgeRepositoryMock.Object,
            _userBadgeRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Execute_AwardsTopTen_RevokesOthers()
    {
        // Arrange
        var topUserIds = Enumerable.Range(1, 10).Select(_ => Guid.NewGuid()).ToList();
        var removedUserId = Guid.NewGuid();

        var leaderboard = topUserIds.Select((id, index) => new LeaderboardEntryDto
        {
            Rank = index + 1,
            UserId = id,
            UserName = $"User-{id}",
            ContributionCount = 10,
            BadgeCount = 2,
            HighestBadgeTier = BadgeTier.Bronze,
            TopBadges = new List<UserBadgeDto>()
        }).ToList();

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<GetBadgeLeaderboardQuery>(q => q.Period == LeaderboardPeriod.Month && q.PageSize == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaderboard);

        var topContributorBadge = CreateBadge("TOP_CONTRIBUTOR");
        _badgeRepositoryMock
            .Setup(r => r.GetByCodeAsync("TOP_CONTRIBUTOR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(topContributorBadge);

        // User no longer in top 10
        var removedUserBadge = UserBadge.Award(removedUserId, topContributorBadge.Id, "TOP_CONTRIBUTOR", null);
        _userBadgeRepositoryMock
            .Setup(r => r.GetUsersByBadgeCodeAsync("TOP_CONTRIBUTOR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserBadge> { removedUserBadge });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RecalculateBadgesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecalculateBadgesResponse
            {
                UsersProcessed = 1,
                BadgesAwarded = 1,
                BadgesRevoked = 0
            });

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        // Verify RecalculateBadgesCommand called for top 10 (award)
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<RecalculateBadgesCommand>(cmd => topUserIds.Contains(cmd.UserId!.Value)),
                It.IsAny<CancellationToken>()),
            Times.Exactly(10));

        // Verify RecalculateBadgesCommand called for removed user (revoke)
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<RecalculateBadgesCommand>(cmd => cmd.UserId == removedUserId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify job result
        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(true)));
    }

    [Fact]
    public async Task Execute_NoContributors_HandlesGracefully()
    {
        // Arrange
        _mediatorMock
            .Setup(m => m.Send(
                It.Is<GetBadgeLeaderboardQuery>(q => q.Period == LeaderboardPeriod.Month),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaderboardEntryDto>());

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<RecalculateBadgesCommand>(), It.IsAny<CancellationToken>()),
            Times.Never); // No badge operations

        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(true) &&
            r.GetType().GetProperty("Awarded")!.GetValue(r)!.Equals(0)));
    }

    [Fact]
    public async Task Execute_MissingBadgeDefinition_LogsErrorAndReturns()
    {
        // Arrange
        var leaderboard = new List<LeaderboardEntryDto>
        {
            new()
            {
                Rank = 1,
                UserId = Guid.NewGuid(),
                UserName = "User1",
                ContributionCount = 10,
                BadgeCount = 1,
                HighestBadgeTier = BadgeTier.Bronze,
                TopBadges = new List<UserBadgeDto>()
            }
        };

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<GetBadgeLeaderboardQuery>(q => q.Period == LeaderboardPeriod.Month),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaderboard);

        _badgeRepositoryMock
            .Setup(r => r.GetByCodeAsync("TOP_CONTRIBUTOR", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Badge?)null); // Badge not found

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<RecalculateBadgesCommand>(), It.IsAny<CancellationToken>()),
            Times.Never); // No operations if badge missing

        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(false)));
    }

    [Fact]
    public async Task Execute_IndividualFailure_ContinuesProcessing()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        var leaderboard = new List<LeaderboardEntryDto>
        {
            new()
            {
                Rank = 1,
                UserId = user1,
                UserName = "User1",
                ContributionCount = 10,
                BadgeCount = 1,
                HighestBadgeTier = BadgeTier.Bronze,
                TopBadges = new List<UserBadgeDto>()
            },
            new()
            {
                Rank = 2,
                UserId = user2,
                UserName = "User2",
                ContributionCount = 9,
                BadgeCount = 1,
                HighestBadgeTier = BadgeTier.Bronze,
                TopBadges = new List<UserBadgeDto>()
            },
            new()
            {
                Rank = 3,
                UserId = user3,
                UserName = "User3",
                ContributionCount = 8,
                BadgeCount = 1,
                HighestBadgeTier = BadgeTier.Bronze,
                TopBadges = new List<UserBadgeDto>()
            }
        };

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<GetBadgeLeaderboardQuery>(q => q.Period == LeaderboardPeriod.Month),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaderboard);

        var topContributorBadge = CreateBadge("TOP_CONTRIBUTOR");
        _badgeRepositoryMock
            .Setup(r => r.GetByCodeAsync("TOP_CONTRIBUTOR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(topContributorBadge);

        _userBadgeRepositoryMock
            .Setup(r => r.GetUsersByBadgeCodeAsync("TOP_CONTRIBUTOR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserBadge>());

        // User2 fails
        _mediatorMock
            .Setup(m => m.Send(
                It.Is<RecalculateBadgesCommand>(cmd => cmd.UserId == user2),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User2 processing failed"));

        // User1 and User3 succeed
        _mediatorMock
            .Setup(m => m.Send(
                It.Is<RecalculateBadgesCommand>(cmd => cmd.UserId == user1 || cmd.UserId == user3),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecalculateBadgesResponse
            {
                UsersProcessed = 1,
                BadgesAwarded = 1,
                BadgesRevoked = 0
            });

        // Act
        await _job.Execute(_jobContextMock.Object);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<RecalculateBadgesCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)); // All 3 users processed despite user2 failure

        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r =>
            r.GetType().GetProperty("Success")!.GetValue(r)!.Equals(true)));
    }

    #region Helper Methods

    private static Badge CreateBadge(string code)
    {
        return Badge.Create(
            code: code,
            name: $"{code} Badge",
            description: "Test badge",
            tier: BadgeTier.Gold,
            category: BadgeCategory.Engagement,
            requirement: BadgeRequirement.ForTopContributor(10));
    }

    #endregion
}