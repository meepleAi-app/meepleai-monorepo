using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #2698: the RSVP roster read must be participant-scoped (organizer or invited), closing the
/// cross-tenant IDOR where any authenticated user could read another user's roster + names.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetGameNightRsvpsQueryHandlerTests : IDisposable
{
    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly MeepleAiDbContext _db;
    private readonly GetGameNightRsvpsQueryHandler _handler;

    public GetGameNightRsvpsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(
            options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new GetGameNightRsvpsQueryHandler(_repo.Object, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private GameNightEvent PublishedEventWith(Guid organizerId, params Guid[] invitedUserIds)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Serata privata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish(invitedUserIds.ToList());
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);
        return evt;
    }

    [Fact]
    public async Task Handle_NonParticipant_ThrowsForbidden()
    {
        // The core IDOR: a random authenticated user must NOT read another user's roster.
        var evt = PublishedEventWith(Guid.NewGuid(), Guid.NewGuid());

        var act = () => _handler.Handle(
            new GetGameNightRsvpsQuery(evt.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_Organizer_ReturnsRoster()
    {
        var organizerId = Guid.NewGuid();
        var evt = PublishedEventWith(organizerId, Guid.NewGuid());

        var result = await _handler.Handle(
            new GetGameNightRsvpsQuery(evt.Id, organizerId), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
    }

    /// <summary>
    /// #4780 — the roster of a night is read by every guest, and a user without a usable name appeared under
    /// their address (<c>DisplayName ?? Email</c>). Nor any other address: the old one, kept as a name after the
    /// address changed, is not the account's.
    /// </summary>
    [Theory]
    [Trait("Issue", "4780")]
    [InlineData(null, "Giocatore")]
    [InlineData("marta.rossi@example.com", "Giocatore")]
    [InlineData("Marta (marta.rossi@example.com)", "Giocatore")]
    [InlineData("marta@vecchio.it", "Giocatore")]
    [InlineData("Marta Rossi", "Marta Rossi")]
    public async Task Handle_TheRosterNeverShowsAGuestsAddress(string? profileName, string shown)
    {
        var organizerId = Guid.NewGuid();
        var invitedId = Guid.NewGuid();
        var evt = PublishedEventWith(organizerId, invitedId);
        _db.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = invitedId,
            Email = "marta.rossi@example.com",
            DisplayName = profileName,
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetGameNightRsvpsQuery(evt.Id, organizerId), TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.UserName.Should().Be(shown);
    }

    [Fact]
    public async Task Handle_InvitedUser_ReturnsRoster()
    {
        var invitedUserId = Guid.NewGuid();
        var evt = PublishedEventWith(Guid.NewGuid(), invitedUserId);

        var result = await _handler.Handle(
            new GetGameNightRsvpsQuery(evt.Id, invitedUserId), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
    }
}
