using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #2633 Slice A: the night-live read model projects the night header + its session progression
/// (status/order/winner/timing) so the FE can wire NightLiveClientView off fixtures.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetGameNightLiveQueryHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly GetGameNightLiveQueryHandler _handler;

    public GetGameNightLiveQueryHandlerTests()
    {
        _handler = new GetGameNightLiveQueryHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ProjectsSessions_OrderedByPlayOrderWithStatuses()
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Serata da Marco", DateTimeOffset.UtcNow.AddHours(1),
            gameIds: [Guid.NewGuid(), Guid.NewGuid()]);
        evt.Publish([]);
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        evt.AddSession(s1, evt.GameIds[0], "Brass: Birmingham"); // PlayOrder 1
        evt.AddSession(s2, evt.GameIds[1], "Spirit Island"); // PlayOrder 2
        evt.StartCurrentSession(); // s1 → InProgress
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id), TestContext.Current.CancellationToken);

        result.Id.Should().Be(evt.Id);
        result.Title.Should().Be("Serata da Marco");
        result.Status.Should().Be(GameNightStatus.Published);
        result.Sessions.Should().HaveCount(2);

        result.Sessions[0].PlayOrder.Should().Be(1);
        result.Sessions[0].SessionId.Should().Be(s1);
        result.Sessions[0].GameTitle.Should().Be("Brass: Birmingham");
        result.Sessions[0].Status.Should().Be(GameNightSessionStatus.InProgress);
        result.Sessions[0].StartedAt.Should().NotBeNull();

        result.Sessions[1].PlayOrder.Should().Be(2);
        result.Sessions[1].GameTitle.Should().Be("Spirit Island");
        result.Sessions[1].Status.Should().Be(GameNightSessionStatus.Pending);
        result.Sessions[1].StartedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NoSessions_ReturnsEmptyList()
    {
        var evt = GameNightEvent.Create(
            Guid.NewGuid(), "Serata vuota", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);

        var result = await _handler.Handle(
            new GetGameNightLiveQuery(evt.Id), TestContext.Current.CancellationToken);

        result.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var act = () => _handler.Handle(
            new GetGameNightLiveQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
