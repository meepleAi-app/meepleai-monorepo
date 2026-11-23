using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// #5136 — the job judges the month just closed: it recalculates everyone ranked within the widest TopContributorRank of
/// the leaderboard badges, and every holder of one of them. RecalculateBadges (with BadgeEvaluator) decides; here it is a
/// double, and these tests are about who the job hands to it, and what it reports.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class TopContributorBadgeJobTests
{
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<IBadgeRepository> _badgeRepositoryMock = new();
    private readonly Mock<IUserBadgeRepository> _userBadgeRepositoryMock = new();
    private readonly Mock<IJobExecutionContext> _jobContextMock = new();
    private readonly TopContributorBadgeJob _job;
    private readonly List<Guid> _recalculated = [];
    private int _scopesOpened;

    public TopContributorBadgeJobTests()
    {
        _jobContextMock.Setup(c => c.CancellationToken).Returns(TestContext.Current.CancellationToken);
        _userBadgeRepositoryMock
            .Setup(r => r.GetUsersByBadgeCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserBadge>());
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RecalculateBadgesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<RecalculateBadgesResponse>, CancellationToken>((c, _) => _recalculated.Add(((RecalculateBadgesCommand)c).UserId!.Value))
            .ReturnsAsync(new RecalculateBadgesResponse { UsersProcessed = 1, BadgesAwarded = 1, BadgesRevoked = 0 });
        GivenTheLeaderboardBadges(("TOP_CONTRIBUTOR", 10), ("TOP_THREE", 3), ("NUMBER_ONE", 1));

        // The recalculations go through a mediator taken from a scope of their own; here every scope hands out the same
        // double, and counts itself.
        var scopes = new ServiceCollection()
            .AddScoped<IMediator>(_ =>
            {
                _scopesOpened++;
                return _mediatorMock.Object;
            })
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        _job = new TopContributorBadgeJob(
            _mediatorMock.Object,
            scopes,
            _badgeRepositoryMock.Object,
            _userBadgeRepositoryMock.Object,
            Mock.Of<ILogger<TopContributorBadgeJob>>());
    }

    [Fact]
    public async Task Execute_RecalculatesTheRankedWithinTheWidestRank_AndNobodyBeyond()
    {
        var first = Guid.NewGuid();
        var tenth = Guid.NewGuid();
        var eleventh = Guid.NewGuid();
        GivenTheRanks((first, 1), (tenth, 10), (eleventh, 11));

        await _job.Execute(_jobContextMock.Object);

        _recalculated.Should().BeEquivalentTo([first, tenth]);
    }

    [Fact]
    public async Task Execute_RecalculatesTheHolderOfAnyLeaderboardBadge_OutOfTheRanking()
    {
        // Last month's NUMBER_ONE may not hold TOP_CONTRIBUTOR; out of the ranking, it is recalculated (and loses it).
        var formerNumberOne = Guid.NewGuid();
        GivenTheRanks((Guid.NewGuid(), 1));
        GivenTheHolders("NUMBER_ONE", formerNumberOne);

        await _job.Execute(_jobContextMock.Object);

        _recalculated.Should().Contain(formerNumberOne);
    }

    [Fact]
    public async Task Execute_WithNoContributorLastMonth_StillRecalculatesTheHolders()
    {
        // Review of #5137: the job returned early on an empty ranking, and last month's holders kept the badges.
        var holder = Guid.NewGuid();
        GivenTheRanks();
        GivenTheHolders("TOP_CONTRIBUTOR", holder);

        await _job.Execute(_jobContextMock.Object);

        _recalculated.Should().Equal(holder);
    }

    [Fact]
    public async Task Execute_TakesTheWidestRankFromTheBadgeDefinitions()
    {
        // A badge with a wider rank is picked up without touching the job.
        GivenTheLeaderboardBadges(("TOP_CONTRIBUTOR", 10), ("TOP_TWENTY", 20));
        var fifteenth = Guid.NewGuid();
        GivenTheRanks((fifteenth, 15));

        await _job.Execute(_jobContextMock.Object);

        _recalculated.Should().Equal(fifteenth);
    }

    [Fact]
    public async Task Execute_ReportsTheAwardsAndTheRevocationsOfEveryCandidate()
    {
        // Review of #5137: the revocations of the users still ranked were dropped from the count.
        GivenTheRanks((Guid.NewGuid(), 1), (Guid.NewGuid(), 5));
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RecalculateBadgesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecalculateBadgesResponse { UsersProcessed = 1, BadgesAwarded = 1, BadgesRevoked = 2 });

        await _job.Execute(_jobContextMock.Object);

        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r => Reported(r, "Awarded", 2) && Reported(r, "Revoked", 4)));
    }

    [Fact]
    public async Task Execute_AFailureForOneCandidate_DoesNotStopTheOthers()
    {
        var failing = Guid.NewGuid();
        var other = Guid.NewGuid();
        GivenTheRanks((failing, 1), (other, 2));
        _mediatorMock
            .Setup(m => m.Send(It.Is<RecalculateBadgesCommand>(c => c.UserId == failing), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _job.Execute(_jobContextMock.Object);

        _recalculated.Should().Contain(other);
        _jobContextMock.VerifySet(c => c.Result = It.Is<object>(r => Reported(r, "Success", true)));
    }

    [Fact]
    public async Task Execute_RecalculatesEachCandidateInAScopeOfItsOwn()
    {
        // Review of #5137: one scope shares one DbContext, and a failed save left its entities tracked, so every later
        // candidate's save retried them and failed too.
        GivenTheRanks((Guid.NewGuid(), 1), (Guid.NewGuid(), 2), (Guid.NewGuid(), 3));

        await _job.Execute(_jobContextMock.Object);

        _recalculated.Should().HaveCount(3);
        _scopesOpened.Should().Be(3);
    }

    [Fact]
    public async Task Execute_WithNoLeaderboardBadge_RecalculatesNobody()
    {
        GivenTheLeaderboardBadges();

        await _job.Execute(_jobContextMock.Object);

        _recalculated.Should().BeEmpty();
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<GetPreviousMonthContributionRanksQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void GivenTheLeaderboardBadges(params (string Code, int Rank)[] badges) =>
        _badgeRepositoryMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(badges
                .Select(b => Badge.Create(b.Code, b.Code, "Test badge", BadgeTier.Gold, BadgeCategory.Engagement,
                    BadgeRequirement.ForTopContributor(b.Rank)))
                .Append(Badge.Create("FIRST_CONTRIBUTION", "First", "Not a leaderboard badge", BadgeTier.Bronze,
                    BadgeCategory.Contribution, BadgeRequirement.ForFirstContribution()))
                .ToList());

    private void GivenTheRanks(params (Guid UserId, int Rank)[] ranks) =>
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetPreviousMonthContributionRanksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, int>)ranks.ToDictionary(r => r.UserId, r => r.Rank));

    private void GivenTheHolders(string code, params Guid[] userIds) =>
        _userBadgeRepositoryMock
            .Setup(r => r.GetUsersByBadgeCodeAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userIds.Select(u => UserBadge.Award(u, Guid.NewGuid(), code, null)).ToList());

    private static bool Reported(object result, string property, object expected) =>
        Equals(result.GetType().GetProperty(property)?.GetValue(result), expected);
}
