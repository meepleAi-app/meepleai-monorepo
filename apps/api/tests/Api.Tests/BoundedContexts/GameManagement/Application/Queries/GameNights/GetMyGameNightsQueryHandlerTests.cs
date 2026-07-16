using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// #2978 (invariante #17): GET /game-nights/mine must carry the viewer's own RSVP status. The
/// query already carries the viewer id (UserId); the handler propagates it to the mapper.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetMyGameNightsQueryHandlerTests : IDisposable
{
    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly MeepleAiDbContext _db;
    private readonly GetMyGameNightsQueryHandler _handler;

    public GetMyGameNightsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(
            options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new GetMyGameNightsQueryHandler(_repo.Object, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static GameNightEvent PublishedEventWith(Guid organizerId, params Guid[] invitedUserIds)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish(invitedUserIds.ToList());
        return evt;
    }

    [Fact]
    public async Task Handle_ViewerIsPendingInvitee_PopulatesMyRsvpStatus()
    {
        var viewerId = Guid.NewGuid();
        var evt = PublishedEventWith(Guid.NewGuid(), viewerId);
        _repo.Setup(r => r.GetByUserAsync(viewerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameNightEvent> { evt });

        var result = await _handler.Handle(
            new GetMyGameNightsQuery(viewerId), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].MyRsvpStatus.Should().Be(RsvpStatus.Pending);
    }

    [Fact]
    public async Task Handle_ViewerIsOrganizer_MyRsvpStatusIsNull()
    {
        var organizerId = Guid.NewGuid();
        var evt = PublishedEventWith(organizerId, Guid.NewGuid());
        _repo.Setup(r => r.GetByUserAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameNightEvent> { evt });

        var result = await _handler.Handle(
            new GetMyGameNightsQuery(organizerId), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].MyRsvpStatus.Should().BeNull();
    }
}
