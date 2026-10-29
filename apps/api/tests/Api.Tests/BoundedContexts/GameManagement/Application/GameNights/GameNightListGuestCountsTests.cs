using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNights;

/// <summary>
/// #4706: the lists of game nights (mine, upcoming, completed) show the same
/// accepted/pending/invited counts as the detail, so they must also count the guests invited
/// by email — one criterion on every surface that shows those numbers.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightListGuestCountsTests : IDisposable
{
    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly MeepleAiDbContext _db;
    private readonly GameNightEvent _first;
    private readonly GameNightEvent _second;
    private readonly Guid _organizerId = Guid.NewGuid();

    public GameNightListGuestCountsTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(
            options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);

        _first = NightWithGuests(accepted: 1, pending: 1);
        _second = NightWithGuests(accepted: 0, pending: 2);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private GameNightEvent NightWithGuests(int accepted, int pending)
    {
        var evt = GameNightEvent.Create(
            _organizerId, "Serata", DateTimeOffset.UtcNow.AddDays(1), gameIds: [Guid.NewGuid()]);
        evt.Publish([]);
        for (var i = 0; i < accepted; i++)
            AddGuest(evt.Id, GameNightInvitationStatus.Accepted);
        for (var i = 0; i < pending; i++)
            AddGuest(evt.Id, GameNightInvitationStatus.Pending);
        return evt;
    }

    private void AddGuest(Guid gameNightId, GameNightInvitationStatus status)
    {
        _db.GameNightInvitations.Add(new GameNightInvitationEntity
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            GameNightId = gameNightId,
            Email = $"{Guid.NewGuid():N}@example.com",
            Status = status.ToString(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _organizerId,
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task GetMyGameNights_CountsTheGuestsOfEachNight()
    {
        _repo.Setup(r => r.GetByUserAsync(_organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_first, _second]);
        var handler = new GetMyGameNightsQueryHandler(_repo.Object, _db);

        var result = await handler.Handle(
            new GetMyGameNightsQuery(_organizerId), TestContext.Current.CancellationToken);

        AssertEachNightHasItsOwnGuests(result);
    }

    [Fact]
    public async Task GetUpcomingGameNights_CountsTheGuestsOfEachNight()
    {
        _repo.Setup(r => r.GetUpcomingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([_first, _second]);
        var handler = new GetUpcomingGameNightsQueryHandler(_repo.Object, _db);

        var result = await handler.Handle(
            new GetUpcomingGameNightsQuery(_organizerId), TestContext.Current.CancellationToken);

        AssertEachNightHasItsOwnGuests(result);
    }

    [Fact]
    public async Task GetCompletedGameNights_CountsTheGuestsOfEachNight()
    {
        _repo.Setup(r => r.GetCompletedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_first, _second]);
        var handler = new GetCompletedGameNightsQueryHandler(_repo.Object, _db);

        var result = await handler.Handle(
            new GetCompletedGameNightsQuery(), TestContext.Current.CancellationToken);

        AssertEachNightHasItsOwnGuests(result);
    }

    private void AssertEachNightHasItsOwnGuests(
        IReadOnlyList<Api.BoundedContexts.GameManagement.Application.DTOs.GameNights.GameNightDto> result)
    {
        var first = result.Single(n => n.Id == _first.Id);
        first.AcceptedCount.Should().Be(1);
        first.PendingCount.Should().Be(1);
        first.TotalInvited.Should().Be(2);

        var second = result.Single(n => n.Id == _second.Id);
        second.AcceptedCount.Should().Be(0);
        second.PendingCount.Should().Be(2);
        second.TotalInvited.Should().Be(2);
    }
}
