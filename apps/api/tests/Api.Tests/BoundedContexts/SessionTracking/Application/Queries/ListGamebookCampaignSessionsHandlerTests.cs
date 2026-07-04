using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// #2632 (SI-1b Phase 3): projects a campaign's sittings, enforcing ownership via
/// <see cref="GetGamebookCampaignQuery"/> and computing IsLive.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class ListGamebookCampaignSessionsHandlerTests
{
    private readonly Mock<ISessionRepository> _sessions = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly ListGamebookCampaignSessionsHandler _handler;

    public ListGamebookCampaignSessionsHandlerTests()
    {
        _handler = new ListGamebookCampaignSessionsHandler(_sessions.Object, _mediator.Object);
    }

    private void SetupOwnershipOk(Guid campaignId, Guid caller)
        => _mediator
            .Setup(m => m.Send(
                It.Is<GetGamebookCampaignQuery>(q => q.CampaignId == campaignId && q.CallerUserId == caller),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GamebookCampaignDto(
                campaignId, Guid.NewGuid(), 0, caller, "La Runa di Eldoria",
                0, Array.Empty<int>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

    [Fact]
    public async Task Handle_ReturnsCampaignSessionIds()
    {
        var campaignId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        SetupOwnershipOk(campaignId, caller);

        var s1 = Session.Create(caller, Guid.NewGuid(), SessionType.Generic, gamebookCampaignId: campaignId);
        var s2 = Session.Create(caller, Guid.NewGuid(), SessionType.Generic, gamebookCampaignId: campaignId);

        _sessions
            .Setup(r => r.ListByGamebookCampaignAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session> { s1, s2 });

        var result = await _handler.Handle(
            new ListGamebookCampaignSessionsQuery(campaignId, caller),
            TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new[] { s1.Id, s2.Id });
    }

    [Fact]
    public async Task Handle_OwnershipQueryThrows_Propagates_AndDoesNotQueryRepo()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetGamebookCampaignQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException("Forbidden"));

        var act = () => _handler.Handle(
            new ListGamebookCampaignSessionsQuery(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        _sessions.Verify(
            r => r.ListByGamebookCampaignAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
