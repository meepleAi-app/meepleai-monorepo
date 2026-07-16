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
/// #2978 (invariante #17): the dashboard "Prossimi" endpoint (GET /game-nights) must carry the
/// viewer's own RSVP status so the FE can render the pending-invitee treatment. The query now
/// carries the caller id; the handler propagates it to the mapper.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetUpcomingGameNightsQueryHandlerTests : IDisposable
{
    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly MeepleAiDbContext _db;
    private readonly GetUpcomingGameNightsQueryHandler _handler;

    public GetUpcomingGameNightsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(
            options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new GetUpcomingGameNightsQueryHandler(_repo.Object, _db);
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
        _repo.Setup(r => r.GetUpcomingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameNightEvent> { evt });

        var result = await _handler.Handle(
            new GetUpcomingGameNightsQuery(viewerId), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].MyRsvpStatus.Should().Be(RsvpStatus.Pending);
    }

    [Fact]
    public async Task Handle_ViewerNotInvited_MyRsvpStatusIsNull()
    {
        var evt = PublishedEventWith(Guid.NewGuid(), Guid.NewGuid());
        _repo.Setup(r => r.GetUpcomingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameNightEvent> { evt });

        var result = await _handler.Handle(
            new GetUpcomingGameNightsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].MyRsvpStatus.Should().BeNull();
    }
}
