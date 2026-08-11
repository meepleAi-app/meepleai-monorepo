using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #2632 (SI-1b Phase 3): the spine read path — derives the owning GameNight "Serata" + campaign
/// status from a campaign's sittings. Liveness comes from the authoritative
/// <c>GameNightSession.Status</c> (InProgress), not <c>Session.StartedAt</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetGamebookCampaignSpineQueryHandlerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly GetGamebookCampaignSpineQueryHandler _handler;

    public GetGamebookCampaignSpineQueryHandlerTests()
    {
        _handler = new GetGamebookCampaignSpineQueryHandler(_mediator.Object, _repo.Object);
    }

    private void SetupSessionIds(Guid campaignId, params Guid[] ids)
        => _mediator
            .Setup(m => m.Send(
                It.Is<ListGamebookCampaignSessionsQuery>(q => q.CampaignId == campaignId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids.ToList());

    /// <summary>A Published GameNight with one sitting; <paramref name="started"/> makes it InProgress (live).</summary>
    private static GameNightEvent WithSession(Guid sessionId, string title, bool started)
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), title, DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        evt.AddSession(sessionId, evt.GameIds[0], "Eldoria");
        if (started)
            evt.StartCurrentSession();
        return evt;
    }

    [Fact]
    public async Task Handle_NoSessions_ReturnsNull()
    {
        var campaignId = Guid.NewGuid();
        SetupSessionIds(campaignId); // empty

        var result = await _handler.Handle(
            new GetGamebookCampaignSpineQuery(campaignId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LiveSitting_ReturnsInProgressSpine()
    {
        var campaignId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupSessionIds(campaignId, sessionId);
        var gameNight = WithSession(sessionId, "Serata da Marco", started: true);
        _repo.Setup(r => r.FindByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);

        var result = await _handler.Handle(
            new GetGamebookCampaignSpineQuery(campaignId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.HasLiveSession.Should().BeTrue();
        result.CampaignStatus.Should().Be("InProgress");
        result.GameNightTitle.Should().Be("Serata da Marco");
        result.GameNightId.Should().Be(gameNight.Id);
        result.TotalSessions.Should().Be(1);
        result.CompletedSessions.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NoLiveSitting_ReturnsResumableSpine()
    {
        var campaignId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupSessionIds(campaignId, sessionId);
        _repo.Setup(r => r.FindByLinkedSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WithSession(sessionId, "Serata", started: false));

        var result = await _handler.Handle(
            new GetGamebookCampaignSpineQuery(campaignId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.HasLiveSession.Should().BeFalse();
        result.CampaignStatus.Should().Be("Resumable");
    }

    [Fact]
    public async Task Handle_SessionsButNoGameNightAttached_ReturnsNull()
    {
        var campaignId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupSessionIds(campaignId, sessionId);
        _repo.Setup(r => r.FindByLinkedSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var result = await _handler.Handle(
            new GetGamebookCampaignSpineQuery(campaignId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LiveSittingPreferredOverOlderNight()
    {
        // Two sittings in different nights: an older non-live and a current live one. The live one wins.
        var campaignId = Guid.NewGuid();
        var oldSession = Guid.NewGuid();
        var liveSession = Guid.NewGuid();
        SetupSessionIds(campaignId, oldSession, liveSession);
        _repo.Setup(r => r.FindByLinkedSessionIdAsync(liveSession, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WithSession(liveSession, "Serata Live", started: true));
        _repo.Setup(r => r.FindByLinkedSessionIdAsync(oldSession, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WithSession(oldSession, "Serata Vecchia", started: false));

        var result = await _handler.Handle(
            new GetGamebookCampaignSpineQuery(campaignId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.GameNightTitle.Should().Be("Serata Live");
        result.HasLiveSession.Should().BeTrue();
        result.CampaignStatus.Should().Be("InProgress");
    }
}
