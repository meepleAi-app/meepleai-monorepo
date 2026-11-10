using Api.BoundedContexts.GameManagement.Application.Queries.Dashboard;
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

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries.Dashboard;

/// <summary>
/// #4943: a registered user who accepted an email invitation while logged in (#4759) has no RSVP row, so
/// for the organizer and the other players they never became a friend, and for them nobody did. A night
/// accepted by email counts as a night accepted with an RSVP, with the same binding as the lists and the
/// detail gate: answered by that account, still accepted, addressed to that account's email.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4943")]
public sealed class GetFriendsActivityEmailGuestsTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly MeepleAiDbContext _db;
    private readonly GetFriendsActivityQueryHandler _sut;

    public GetFriendsActivityEmailGuestsTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"FriendsEmailGuests_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _sut = new GetFriendsActivityQueryHandler(_db);
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

    private GameNightEventEntity Night(Guid organizerId, string title, string status = "Published", int daysAgo = 2)
    {
        var night = new GameNightEventEntity
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = title,
            ScheduledAt = Now.AddDays(-daysAgo),
            GameIdsJson = "[]",
            Status = status,
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
            RespondedAt = Now.AddDays(-3),
            CreatedAt = Now.AddDays(-4),
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
            RespondedAt = respondedBy is null ? null : Now.AddDays(-3),
            ExpiresAt = Now.AddDays(7),
            CreatedAt = Now.AddDays(-5),
            CreatedBy = Guid.NewGuid(),
        });

    private async Task<IReadOnlyList<Api.BoundedContexts.GameManagement.Application.DTOs.FriendActivityDto>> FeedOf(Guid userId)
    {
        await _db.SaveChangesAsync();
        return await _sut.Handle(new GetFriendsActivityQuery(userId), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheGuestWhoAcceptedByEmail_IsAFriendOfTheOrganizer_AndJoinedTheNight()
    {
        var organizer = User("Marta");
        var guest = User("Giulia");
        var night = Night(organizer.Id, "Venerdì Catan");
        Invitation(night.Id, guest.Id, guest.Email);

        var feed = await FeedOf(organizer.Id);

        feed.Should().ContainSingle(a => a.FriendUserId == guest.Id && a.Verb == "joined" && a.GameOrEventId == night.Id,
            "a seat taken by email is a seat taken");
    }

    [Fact]
    public async Task TheOrganizer_IsAFriendOfTheGuestWhoAcceptedByEmail()
    {
        var organizer = User("Marta");
        var guest = User("Giulia");
        var night = Night(organizer.Id, "Venerdì Catan");
        Invitation(night.Id, guest.Id, guest.Email);

        var feed = await FeedOf(guest.Id);

        feed.Should().ContainSingle(a => a.FriendUserId == organizer.Id && a.Verb == "created" && a.GameOrEventId == night.Id,
            "the guest sits at that table, so its organizer is somebody they play with");
    }

    [Fact]
    public async Task TheOtherPlayersOfThatNight_AreFriendsOfTheGuest_AndTheGuestOfThem()
    {
        var organizer = User("Marta");
        var member = User("Luca");
        var guest = User("Giulia");
        var night = Night(organizer.Id, "Venerdì Catan");
        Rsvp(night.Id, member.Id);
        Invitation(night.Id, guest.Id, guest.Email);

        (await FeedOf(guest.Id)).Should().Contain(a => a.FriendUserId == member.Id);
        (await FeedOf(member.Id)).Should().Contain(a => a.FriendUserId == guest.Id);
    }

    [Fact]
    public async Task ADeclinedInvitation_DoesNotMakeAFriend()
    {
        var organizer = User("Marta");
        var guest = User("Giulia");
        var night = Night(organizer.Id, "Venerdì Catan");
        Invitation(night.Id, guest.Id, guest.Email, status: "Declined");

        (await FeedOf(organizer.Id)).Should().BeEmpty();
        (await FeedOf(guest.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task AForwardedLink_AnsweredByAnotherAccount_DoesNotMakeAFriend()
    {
        // The invitation is addressed to somebody else: the account that answered it is not seated (#4759).
        var organizer = User("Marta");
        var forwarder = User("Giulia");
        var night = Night(organizer.Id, "Venerdì Catan");
        Invitation(night.Id, forwarder.Id, "somebody.else@example.com");

        (await FeedOf(organizer.Id)).Should().BeEmpty();
        (await FeedOf(forwarder.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheAddressBinding_IgnoresCaseAndSpaces_AsTheInvitationsNormalize()
    {
        var organizer = User("Marta");
        var guest = User("Giulia", email: " Giulia.Rossi@Example.com ");
        var night = Night(organizer.Id, "Venerdì Catan");
        Invitation(night.Id, guest.Id, "giulia.rossi@example.com");

        (await FeedOf(organizer.Id)).Should().ContainSingle(a => a.FriendUserId == guest.Id);
    }

    [Fact]
    public async Task AnRsvpRowAndAnAcceptedInvitation_OnTheSameNight_JoinItOnce()
    {
        var organizer = User("Marta");
        var both = User("Giulia");
        var night = Night(organizer.Id, "Venerdì Catan");
        Rsvp(night.Id, both.Id);
        Invitation(night.Id, both.Id, both.Email);

        var feed = await FeedOf(organizer.Id);

        feed.Where(a => a.FriendUserId == both.Id && a.Verb == "joined").Should().ContainSingle();
    }

    [Fact]
    public async Task AFriendsNightJoinedByEmailOnly_IsLoadedAsAnActivity_EvenIfNobodyThereHasAnRsvpRow()
    {
        // Step 2 loads the nights a friend touched; a night the friend joined only by email has no RSVP row to
        // find it by, and used to be missed.
        var me = User("Marta");
        var friend = User("Giulia");
        var host = User("Pietro");
        var shared = Night(me.Id, "La nostra serata");
        Rsvp(shared.Id, friend.Id);
        var elsewhere = Night(host.Id, "Da Pietro");
        Invitation(elsewhere.Id, friend.Id, friend.Email);

        var feed = await FeedOf(me.Id);

        feed.Should().Contain(a => a.FriendUserId == friend.Id && a.Verb == "joined" && a.GameOrEventId == elsewhere.Id);
    }
}
