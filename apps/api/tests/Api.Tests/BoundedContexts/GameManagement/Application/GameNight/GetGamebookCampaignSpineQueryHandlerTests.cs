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
/// status from a campaign's sittings.
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

    private void SetupSittings(Guid campaignId, params GamebookSittingDto[] sittings)
        => _mediator
            .Setup(m => m.Send(
                It.Is<ListGamebookCampaignSessionsQuery>(q => q.CampaignId == campaignId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sittings.ToList());

    private static GameNightEvent PublishedWithSession(Guid sessionId, string title = "Serata da Marco")
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), title, DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        evt.AddSession(sessionId, evt.GameIds[0], "Eldoria");
        return evt;
    }

    [Fact]
    public async Task Handle_NoSittings_ReturnsNull()
    {
        var campaignId = Guid.NewGuid();
        SetupSittings(campaignId); // empty

        var result = await _handler.Handle(
            new GetGamebookCampaignSpineQuery(campaignId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LiveSitting_ReturnsInProgressSpine()
    {
        var campaignId = Guid.NewGuid();
        var sittingId = Guid.NewGuid();
        SetupSittings(campaignId, new GamebookSittingDto(sittingId, IsLive: true, DateTime.UtcNow));
        var gameNight = PublishedWithSession(sittingId);
        _repo.Setup(r => r.FindByLinkedSessionIdAsync(sittingId, It.IsAny<CancellationToken>()))
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
        var sittingId = Guid.NewGuid();
        SetupSittings(campaignId, new GamebookSittingDto(sittingId, IsLive: false, DateTime.UtcNow.AddDays(-1)));
        _repo.Setup(r => r.FindByLinkedSessionIdAsync(sittingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublishedWithSession(sittingId));

        var result = await _handler.Handle(
            new GetGamebookCampaignSpineQuery(campaignId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.HasLiveSession.Should().BeFalse();
        result.CampaignStatus.Should().Be("Resumable");
    }

    [Fact]
    public async Task Handle_SittingsButNoGameNightAttached_ReturnsNull()
    {
        var campaignId = Guid.NewGuid();
        var sittingId = Guid.NewGuid();
        SetupSittings(campaignId, new GamebookSittingDto(sittingId, IsLive: false, DateTime.UtcNow));
        _repo.Setup(r => r.FindByLinkedSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var result = await _handler.Handle(
            new GetGamebookCampaignSpineQuery(campaignId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LiveSittingPreferredOverOlder()
    {
        // Two sittings: an older non-live and a current live one. The live one's GameNight wins.
        var campaignId = Guid.NewGuid();
        var oldSitting = Guid.NewGuid();
        var liveSitting = Guid.NewGuid();
        SetupSittings(campaignId,
            new GamebookSittingDto(oldSitting, IsLive: false, DateTime.UtcNow.AddDays(-2)),
            new GamebookSittingDto(liveSitting, IsLive: true, DateTime.UtcNow));
        _repo.Setup(r => r.FindByLinkedSessionIdAsync(liveSitting, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublishedWithSession(liveSitting, "Serata Live"));
        _repo.Setup(r => r.FindByLinkedSessionIdAsync(oldSitting, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublishedWithSession(oldSitting, "Serata Vecchia"));

        var result = await _handler.Handle(
            new GetGamebookCampaignSpineQuery(campaignId, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.GameNightTitle.Should().Be("Serata Live");
        result.HasLiveSession.Should().BeTrue();
    }
}
