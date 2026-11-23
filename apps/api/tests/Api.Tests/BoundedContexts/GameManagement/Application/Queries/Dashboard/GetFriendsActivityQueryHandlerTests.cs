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
/// Unit tests for <see cref="GetFriendsActivityQueryHandler"/>.
/// Asse C (#1898) WP1 T1 DEC-2: dashboard "Cosa fanno i tuoi" friends activity feed.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GetFriendsActivityQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetFriendsActivityQueryHandler _sut;

    public GetFriendsActivityQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"GetFriendsActivity_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _sut = new GetFriendsActivityQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    // ──────────────────────────────────────────────────────────────────────
    // Seed helpers
    // ──────────────────────────────────────────────────────────────────────

    private async Task<UserEntity> SeedUserAsync(string? displayName = null, string? avatarUrl = null)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@example.com",
            DisplayName = displayName,
            PasswordHash = "x",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            AvatarUrl = avatarUrl,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task<GameNightEventEntity> SeedNightAsync(
        Guid organizerId,
        string title,
        string status = "Published",
        DateTimeOffset? scheduledAt = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        var effectiveScheduled = scheduledAt ?? DateTimeOffset.UtcNow.AddDays(-2);
        var effectiveCreated = createdAt ?? effectiveScheduled.AddDays(-7);
        var night = new GameNightEventEntity
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = title,
            ScheduledAt = effectiveScheduled,
            GameIdsJson = "[]",
            Status = status,
            CreatedAt = effectiveCreated,
            UpdatedAt = updatedAt,
        };
        _dbContext.GameNightEvents.Add(night);
        await _dbContext.SaveChangesAsync();
        return night;
    }

    private async Task SeedRsvpAsync(
        Guid eventId,
        Guid userId,
        string status = "Accepted",
        DateTimeOffset? respondedAt = null,
        DateTimeOffset? createdAt = null)
    {
        var rsvp = new GameNightRsvpEntity
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = status,
            RespondedAt = respondedAt,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow.AddDays(-3),
        };
        _dbContext.GameNightRsvps.Add(rsvp);
        await _dbContext.SaveChangesAsync();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NoSharedGameNights_ReturnsEmpty()
    {
        var user = await SeedUserAsync("Alice");
        var query = new GetFriendsActivityQuery(user.Id);

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FriendCreatedGameNight_ReturnsActivityWithCreatedVerb()
    {
        // Arrange: user has a shared night with friend (friend is organizer of a
        // Published night).
        var user = await SeedUserAsync("Alice");
        var friend = await SeedUserAsync("Bob", avatarUrl: "https://cdn/bob.png");

        // The shared night where user is RSVP'd to friend's published event.
        var night = await SeedNightAsync(friend.Id, "Bob's Night", status: "Published");
        await SeedRsvpAsync(night.Id, user.Id);

        var query = new GetFriendsActivityQuery(user.Id);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.FriendUserId.Should().Be(friend.Id);
        dto.Verb.Should().Be("created");
        dto.GameOrEventType.Should().Be("gameNight");
        dto.GameOrEventId.Should().Be(night.Id);
        dto.GameOrEventName.Should().Be("Bob's Night");
        dto.Avatar.Should().Be("https://cdn/bob.png");
        dto.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task Handle_FriendCompletedGameNight_ReturnsActivityWithCompletedVerb()
    {
        var user = await SeedUserAsync("Alice");
        var friend = await SeedUserAsync("Bob");

        // Friend organized AND completed a night, user RSVP'd to qualify as friend.
        var night = await SeedNightAsync(
            friend.Id,
            "Completed Night",
            status: "Completed",
            updatedAt: DateTimeOffset.UtcNow.AddHours(-1));
        await SeedRsvpAsync(night.Id, user.Id);

        var query = new GetFriendsActivityQuery(user.Id);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Verb.Should().Be("completed");
        result[0].FriendUserId.Should().Be(friend.Id);
    }

    [Fact]
    public async Task Handle_FriendJoinedGameNight_ReturnsActivityWithJoinedVerb()
    {
        // Arrange: user organizes night #1 where friend is an RSVP'd participant
        // (this qualifies them as a friend AND as a "joined" activity).
        var user = await SeedUserAsync("Alice");
        var friend = await SeedUserAsync("Bob");

        var night = await SeedNightAsync(user.Id, "Alice's Night", status: "Published");
        await SeedRsvpAsync(
            night.Id,
            friend.Id,
            status: "Accepted",
            respondedAt: DateTimeOffset.UtcNow.AddHours(-2));

        var query = new GetFriendsActivityQuery(user.Id);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Verb.Should().Be("joined");
        dto.FriendUserId.Should().Be(friend.Id);
        dto.GameOrEventId.Should().Be(night.Id);
        dto.GameOrEventName.Should().Be("Alice's Night");
    }

    [Fact]
    public async Task Handle_RespectsLimitParameter()
    {
        // Arrange: 6 friends, 6 nights, each friend organizes 1 night with user RSVP'd.
        var user = await SeedUserAsync("Alice");

        var friends = new List<UserEntity>();
        for (var i = 0; i < 6; i++)
        {
            var f = await SeedUserAsync($"Friend{i}");
            friends.Add(f);
            var night = await SeedNightAsync(
                f.Id,
                $"Night {i}",
                status: "Published",
                updatedAt: DateTimeOffset.UtcNow.AddHours(-i));
            await SeedRsvpAsync(night.Id, user.Id);
        }

        var query = new GetFriendsActivityQuery(user.Id, Limit: 3);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);

        // Verify descending timestamp ordering (most recent first).
        result.Select(a => a.Timestamp)
            .Should()
            .BeInDescendingOrder();
    }

    [Fact]
    public async Task Handle_FiltersOutGameNightsOlderThan90Days()
    {
        // Arrange: friend organized a night scheduled 100 days ago.
        var user = await SeedUserAsync("Alice");
        var oldFriend = await SeedUserAsync("Bob");

        var oldNight = await SeedNightAsync(
            oldFriend.Id,
            "Ancient Night",
            scheduledAt: DateTimeOffset.UtcNow.AddDays(-100));
        await SeedRsvpAsync(oldNight.Id, user.Id);

        var query = new GetFriendsActivityQuery(user.Id);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert: oldFriend is not a "friend" because their only shared night is
        // outside the 90d window. No activities are returned.
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExcludesSelfFromFriends()
    {
        // Arrange: user organizes a solo night (no other participants).
        var user = await SeedUserAsync("Alice");
        var night = await SeedNightAsync(user.Id, "Solo Night", status: "Published");

        // No RSVPs from other users — user has no friends.
        var query = new GetFriendsActivityQuery(user.Id);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert: user is not surfaced as their own friend.
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_EmptyUserId_ReturnsEmpty()
    {
        // Arrange: even with seeded data, empty user id should short-circuit.
        var alice = await SeedUserAsync("Alice");
        var bob = await SeedUserAsync("Bob");
        var night = await SeedNightAsync(bob.Id, "Bob's Night");
        await SeedRsvpAsync(night.Id, alice.Id);

        var query = new GetFriendsActivityQuery(Guid.Empty);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FriendWithoutDisplayName_FallsBackToEmailLocalPart()
    {
        // Arrange: friend has null DisplayName but Email="bob@example.com" → "bob".
        var user = await SeedUserAsync("Alice");
        var friend = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "bob@example.com",
            DisplayName = null,
            PasswordHash = "x",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Users.Add(friend);
        await _dbContext.SaveChangesAsync();

        var night = await SeedNightAsync(friend.Id, "Bob's Night");
        await SeedRsvpAsync(night.Id, user.Id);

        var query = new GetFriendsActivityQuery(user.Id);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("bob");
        result[0].Avatar.Should().BeEmpty();
    }

    [Fact]
    [Trait("Issue", "4760")]
    public async Task Handle_FriendsNightTheUserIsNotAt_DoesNotSurface()
    {
        // #4760, decision of 2026-11-23: the feed shows only the nights the viewer sits at, the rule of the
        // detail (403 otherwise) and of the lists (#4748). Bob is Alice's friend through Charlie's night; his
        // own night, where Alice is not invited, stays out: neither its title nor its id reach her.
        var alice = await SeedUserAsync("Alice");
        var bob = await SeedUserAsync("Bob");
        var charlie = await SeedUserAsync("Charlie");

        var sharedNight = await SeedNightAsync(
            charlie.Id,
            "Charlie's Night",
            scheduledAt: DateTimeOffset.UtcNow.AddDays(-10));
        await SeedRsvpAsync(sharedNight.Id, alice.Id);
        await SeedRsvpAsync(sharedNight.Id, bob.Id);

        var bobsNight = await SeedNightAsync(
            bob.Id,
            "Bob's Solo Night",
            status: "Published",
            updatedAt: DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _sut.Handle(new GetFriendsActivityQuery(alice.Id), CancellationToken.None);

        result.Should().NotContain(a => a.GameOrEventId == bobsNight.Id);
        result.Should().NotContain(a => a.GameOrEventName == "Bob's Solo Night");
        result.Should().Contain(a =>
            a.FriendUserId == bob.Id
            && a.Verb == "joined"
            && a.GameOrEventId == sharedNight.Id,
            "Bob is still Alice's friend, and what he did at her table is still shown");
    }

    [Fact]
    [Trait("Issue", "4760")]
    public async Task Handle_FriendsDraftNight_DoesNotSurface_EvenIfTheUserIsInvited()
    {
        // #4760: a Draft is not activity yet, for anybody. The issue's case: Marco prepares in draft a
        // surprise night; nobody sees it in their feed, invited or not.
        var luca = await SeedUserAsync("Luca");
        var marco = await SeedUserAsync("Marco");

        var playedTogether = await SeedNightAsync(marco.Id, "Venerdì Catan", status: "Completed");
        await SeedRsvpAsync(playedTogether.Id, luca.Id);

        var draft = await SeedNightAsync(
            marco.Id,
            "Sorpresa per Giulia",
            status: "Draft",
            updatedAt: DateTimeOffset.UtcNow.AddHours(-1));
        await SeedRsvpAsync(draft.Id, luca.Id, status: "Pending");

        var result = await _sut.Handle(new GetFriendsActivityQuery(luca.Id), CancellationToken.None);

        result.Should().NotContain(a => a.GameOrEventId == draft.Id);
        result.Should().ContainSingle(a => a.GameOrEventId == playedTogether.Id && a.Verb == "completed");
    }

    [Fact]
    [Trait("Issue", "4760")]
    public async Task Handle_TheUsersOwnDraft_DoesNotSurface_EvenWithAFriendWhoAccepted()
    {
        var alice = await SeedUserAsync("Alice");
        var bob = await SeedUserAsync("Bob");

        var shared = await SeedNightAsync(alice.Id, "Alice's Night", status: "Published");
        await SeedRsvpAsync(shared.Id, bob.Id);

        var draft = await SeedNightAsync(alice.Id, "Alice's Draft", status: "Draft");
        await SeedRsvpAsync(draft.Id, bob.Id, respondedAt: DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _sut.Handle(new GetFriendsActivityQuery(alice.Id), CancellationToken.None);

        result.Should().NotContain(a => a.GameOrEventId == draft.Id);
        result.Should().Contain(a => a.FriendUserId == bob.Id && a.GameOrEventId == shared.Id);
    }

    [Fact]
    [Trait("Issue", "4760")]
    public async Task Handle_ADraftMakesNobodyAFriend()
    {
        // #4760: a Draft is not activity, so it does not pick the friends either. Luca and Marco last played
        // together outside the window; Marco's draft, where Luca is invited, must not bring Marco back as a
        // friend and, with him, Marco's join at Luca's old table.
        var luca = await SeedUserAsync("Luca");
        var marco = await SeedUserAsync("Marco");

        var oldNight = await SeedNightAsync(luca.Id, "Da Luca", scheduledAt: DateTimeOffset.UtcNow.AddDays(-120));
        await SeedRsvpAsync(oldNight.Id, marco.Id, respondedAt: DateTimeOffset.UtcNow.AddDays(-125));

        var draft = await SeedNightAsync(
            marco.Id,
            "Sorpresa per Giulia",
            status: "Draft",
            scheduledAt: DateTimeOffset.UtcNow.AddDays(5));
        await SeedRsvpAsync(draft.Id, luca.Id, status: "Pending");

        var result = await _sut.Handle(new GetFriendsActivityQuery(luca.Id), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PendingRsvp_DoesNotSurfaceAsJoined()
    {
        // Arrange: user organized a night, friend's RSVP is Pending (not Accepted).
        var user = await SeedUserAsync("Alice");
        var friend = await SeedUserAsync("Bob");
        var night = await SeedNightAsync(user.Id, "Alice's Night");
        await SeedRsvpAsync(night.Id, friend.Id, status: "Pending");

        var query = new GetFriendsActivityQuery(user.Id);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert: Bob is a friend (shared night with Pending RSVP qualifies),
        // but his Pending RSVP does NOT produce a "joined" activity.
        // Bob's only organized nights... none. So no activities surface for Bob.
        result.Where(a => a.FriendUserId == friend.Id && a.Verb == "joined")
            .Should().BeEmpty();
    }
}
