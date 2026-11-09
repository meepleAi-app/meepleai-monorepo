using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// #4943: a registered user who accepted the organizer's email invitation while logged in has no RSVP row,
/// so they never became a regular. A night accepted by email counts as a night accepted with an RSVP, with
/// the same binding as the lists (answered by that account, still accepted, addressed to its email), and a
/// night with both an RSVP row and an accepted invitation counts once.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4943")]
public sealed class GetRegularsEmailGuestsTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly MeepleAiDbContext _db;
    private readonly GetRegularsQueryHandler _sut;

    public GetRegularsEmailGuestsTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"RegularsEmailGuests_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _sut = new GetRegularsQueryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    private UserEntity User(string name, string? email = null)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email ?? $"{name.ToLowerInvariant()}@example.com",
            DisplayName = name,
            PasswordHash = "x",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        return user;
    }

    private GameNightEventEntity PastNight(Guid organizerId, int daysAgo)
    {
        var night = new GameNightEventEntity
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Serata",
            ScheduledAt = Now.AddDays(-daysAgo),
            GameIdsJson = "[]",
            Status = "Completed",
            CreatedAt = Now.AddDays(-daysAgo - 7),
        };
        _db.GameNightEvents.Add(night);
        return night;
    }

    private void Rsvp(Guid nightId, Guid userId, string status = "Accepted") =>
        _db.GameNightRsvps.Add(new GameNightRsvpEntity
        {
            Id = Guid.NewGuid(),
            EventId = nightId,
            UserId = userId,
            Status = status,
            CreatedAt = Now.AddDays(-10),
        });

    private void Invitation(Guid nightId, Guid? respondedBy, string email, string status = "Accepted") =>
        _db.GameNightInvitations.Add(new GameNightInvitationEntity
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            GameNightId = nightId,
            Email = email,
            Status = status,
            RespondedByUserId = respondedBy,
            RespondedAt = respondedBy is null ? null : Now.AddDays(-9),
            ExpiresAt = Now.AddDays(7),
            CreatedAt = Now.AddDays(-10),
            CreatedBy = Guid.NewGuid(),
        });

    private async Task<IReadOnlyList<Api.BoundedContexts.GameManagement.Application.DTOs.GameNights.RegularDto>> RegularsOf(Guid organizerId)
    {
        await _db.SaveChangesAsync();
        return await _sut.Handle(new GetRegularsQuery(organizerId), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AGuestWhoAcceptedByEmail_IsARegular_WithThatNightCounted()
    {
        var organizer = User("Marta");
        var guest = User("Giulia");
        var night = PastNight(organizer.Id, daysAgo: 5);
        Invitation(night.Id, guest.Id, guest.Email);

        var regulars = await RegularsOf(organizer.Id);

        var giulia = regulars.Should().ContainSingle(r => r.Id == guest.Id).Subject;
        giulia.EventCount.Should().Be(1);
        giulia.LastInvitedAt.Should().Be(night.ScheduledAt);
        giulia.DisplayName.Should().Be("Giulia");
    }

    [Fact]
    public async Task NightsByEmailAndByRsvp_AddUp_AndRankTogether()
    {
        var organizer = User("Marta");
        var giulia = User("Giulia");
        var luca = User("Luca");
        var n1 = PastNight(organizer.Id, 30);
        var n2 = PastNight(organizer.Id, 20);
        var n3 = PastNight(organizer.Id, 10);
        Rsvp(n1.Id, giulia.Id);
        Invitation(n2.Id, giulia.Id, giulia.Email);
        Invitation(n3.Id, giulia.Id, giulia.Email);
        Rsvp(n1.Id, luca.Id);

        var regulars = await RegularsOf(organizer.Id);

        regulars.Select(r => (r.Id, r.EventCount)).Should().Equal((giulia.Id, 3), (luca.Id, 1));
    }

    [Fact]
    public async Task TheSameNight_WithAnRsvpRowAndAnAcceptedInvitation_CountsOnce()
    {
        var organizer = User("Marta");
        var both = User("Giulia");
        var night = PastNight(organizer.Id, 5);
        Rsvp(night.Id, both.Id);
        Invitation(night.Id, both.Id, both.Email);

        (await RegularsOf(organizer.Id)).Single(r => r.Id == both.Id).EventCount.Should().Be(1);
    }

    [Fact]
    public async Task ADeclinedInvitation_OrAForwardedLink_DoesNotMakeARegular()
    {
        var organizer = User("Marta");
        var declined = User("Giulia");
        var forwarder = User("Luca");
        var night = PastNight(organizer.Id, 5);
        Invitation(night.Id, declined.Id, declined.Email, status: "Declined");
        Invitation(night.Id, forwarder.Id, "somebody.else@example.com");

        (await RegularsOf(organizer.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnInvitationAcceptedAnonymously_HasNoAccountToBecomeARegular()
    {
        var organizer = User("Marta");
        var night = PastNight(organizer.Id, 5);
        Invitation(night.Id, respondedBy: null, "anon@example.com");

        (await RegularsOf(organizer.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheWindow_AppliesToEmailGuestsToo()
    {
        var organizer = User("Marta");
        var guest = User("Giulia");
        var old = PastNight(organizer.Id, daysAgo: 400);
        Invitation(old.Id, guest.Id, guest.Email);

        (await RegularsOf(organizer.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnotherOrganizersNight_DoesNotCount()
    {
        var organizer = User("Marta");
        var other = User("Pietro");
        var guest = User("Giulia");
        var night = PastNight(other.Id, 5);
        Invitation(night.Id, guest.Id, guest.Email);

        (await RegularsOf(organizer.Id)).Should().BeEmpty();
    }
}
