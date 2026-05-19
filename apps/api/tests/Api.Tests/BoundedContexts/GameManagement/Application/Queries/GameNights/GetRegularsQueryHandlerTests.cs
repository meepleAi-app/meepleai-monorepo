using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
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
/// Unit tests for <see cref="GetRegularsQueryHandler"/>.
/// Issue #950 (W1-PR2): /game-nights/regulars — spec §7b.2 + §12b BE-6.
/// Verifies aggregation of past invitees: event_count ordering, 12-month window,
/// organizer scoping, and limit clamping.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GetRegularsQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetRegularsQueryHandler _sut;

    public GetRegularsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"GetRegularsTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _sut = new GetRegularsQueryHandler(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private async Task<UserEntity> SeedUserAsync(string? displayName = null, string? email = null)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email ?? $"{Guid.NewGuid():N}@example.com",
            DisplayName = displayName ?? "Test User",
            PasswordHash = "x",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task<GameNightEventEntity> SeedGameNightAsync(
        Guid organizerId,
        DateTimeOffset? scheduledAt = null)
    {
        // Past by default so the regulars handler's "past + window" filter
        // (PR #1294 review fix) admits the seeded events unless a test explicitly
        // overrides scheduledAt to a future date to validate exclusion.
        var effective = scheduledAt ?? DateTimeOffset.UtcNow.AddDays(-1);
        var gn = new GameNightEventEntity
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Test Night",
            ScheduledAt = effective,
            GameIdsJson = "[]",
            Status = "Draft",
            CreatedAt = effective,
        };
        _dbContext.GameNightEvents.Add(gn);
        await _dbContext.SaveChangesAsync();
        return gn;
    }

    private async Task SeedRsvpAsync(
        Guid eventId,
        Guid userId,
        DateTimeOffset? createdAt = null,
        string status = "Pending")
    {
        var rsvp = new GameNightRsvpEntity
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = status,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        };
        _dbContext.GameNightRsvps.Add(rsvp);
        await _dbContext.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Empty state
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoInvitations_ReturnsEmpty()
    {
        var organizer = await SeedUserAsync();

        var result = await _sut.Handle(new GetRegularsQuery(organizer.Id), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Ranking by event count (BE-6)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RanksByEventCountDescending()
    {
        var organizer = await SeedUserAsync();
        var alice = await SeedUserAsync("Alice");
        var bob = await SeedUserAsync("Bob");

        // Alice invited 3 times, Bob 1
        var gn1 = await SeedGameNightAsync(organizer.Id);
        var gn2 = await SeedGameNightAsync(organizer.Id);
        var gn3 = await SeedGameNightAsync(organizer.Id);
        await SeedRsvpAsync(gn1.Id, alice.Id);
        await SeedRsvpAsync(gn2.Id, alice.Id);
        await SeedRsvpAsync(gn3.Id, alice.Id);
        await SeedRsvpAsync(gn1.Id, bob.Id);

        var result = await _sut.Handle(new GetRegularsQuery(organizer.Id), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(alice.Id);
        result[0].EventCount.Should().Be(3);
        result[1].Id.Should().Be(bob.Id);
        result[1].EventCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnEventCountTie_RanksByLastInvitedAtDescending()
    {
        var organizer = await SeedUserAsync();
        var alice = await SeedUserAsync("Alice");
        var bob = await SeedUserAsync("Bob");
        var gn1 = await SeedGameNightAsync(organizer.Id);
        var gn2 = await SeedGameNightAsync(organizer.Id);

        // Both invited once; Bob more recently
        await SeedRsvpAsync(gn1.Id, alice.Id, DateTimeOffset.UtcNow.AddDays(-30));
        await SeedRsvpAsync(gn2.Id, bob.Id, DateTimeOffset.UtcNow.AddDays(-1));

        var result = await _sut.Handle(new GetRegularsQuery(organizer.Id), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(bob.Id);
        result[1].Id.Should().Be(alice.Id);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 12-month window — based on game night ScheduledAt (PR #1294 review fix)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExcludesGameNightsScheduledMoreThan12MonthsAgo()
    {
        var organizer = await SeedUserAsync();
        var old = await SeedUserAsync("OldFriend");
        var recent = await SeedUserAsync("RecentFriend");

        // Old event: scheduledAt 13 months ago (regardless of rsvp.CreatedAt) → EXCLUDED
        var oldNight = await SeedGameNightAsync(organizer.Id, scheduledAt: DateTimeOffset.UtcNow.AddMonths(-13));
        await SeedRsvpAsync(oldNight.Id, old.Id, createdAt: DateTimeOffset.UtcNow.AddMonths(-13));

        // Recent event: scheduledAt 1 month ago → INCLUDED
        var recentNight = await SeedGameNightAsync(organizer.Id, scheduledAt: DateTimeOffset.UtcNow.AddMonths(-1));
        await SeedRsvpAsync(recentNight.Id, recent.Id);

        var result = await _sut.Handle(new GetRegularsQuery(organizer.Id), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(recent.Id);
    }

    [Fact]
    public async Task Handle_ExcludesFutureGameNights()
    {
        // Spec §7b.2: regulars come from PAST game nights. Future-scheduled events
        // should not contribute to the "current play patterns" ranking.
        var organizer = await SeedUserAsync();
        var futureInvitee = await SeedUserAsync("FutureFriend");
        var pastInvitee = await SeedUserAsync("PastFriend");

        var futureNight = await SeedGameNightAsync(organizer.Id, scheduledAt: DateTimeOffset.UtcNow.AddDays(30));
        await SeedRsvpAsync(futureNight.Id, futureInvitee.Id);

        var pastNight = await SeedGameNightAsync(organizer.Id, scheduledAt: DateTimeOffset.UtcNow.AddDays(-7));
        await SeedRsvpAsync(pastNight.Id, pastInvitee.Id);

        var result = await _sut.Handle(new GetRegularsQuery(organizer.Id), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(pastInvitee.Id);
    }

    // ────────────────────────────────────────────────────────────────────────
    // RSVP status filtering (PR #1294 review fix)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExcludesDeclinedRsvps()
    {
        // A user who declined every invitation is not a "regular" by play patterns.
        var organizer = await SeedUserAsync();
        var declined = await SeedUserAsync("Declined");
        var attending = await SeedUserAsync("Attending");
        var pastNight = await SeedGameNightAsync(organizer.Id, scheduledAt: DateTimeOffset.UtcNow.AddDays(-7));

        await SeedRsvpAsync(pastNight.Id, declined.Id, status: "Declined");
        await SeedRsvpAsync(pastNight.Id, attending.Id, status: "Accepted");

        var result = await _sut.Handle(new GetRegularsQuery(organizer.Id), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(attending.Id);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Organizer self-exclusion (PR #1294 review fix)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExcludesOrganizerSelfFromOwnRegulars()
    {
        // Edge case: organizer was added to their own RSVPs list (legacy seed data
        // or accidental self-invite). They should never appear in their own
        // regulars suggestion list.
        var organizer = await SeedUserAsync("Organizer");
        var friend = await SeedUserAsync("Friend");
        var pastNight = await SeedGameNightAsync(organizer.Id, scheduledAt: DateTimeOffset.UtcNow.AddDays(-7));

        await SeedRsvpAsync(pastNight.Id, organizer.Id, status: "Accepted");
        await SeedRsvpAsync(pastNight.Id, friend.Id, status: "Accepted");

        var result = await _sut.Handle(new GetRegularsQuery(organizer.Id), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(friend.Id);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Organizer scoping
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OnlyAggregatesRsvpsForOrganizersGameNights()
    {
        var marco = await SeedUserAsync("Marco");
        var laura = await SeedUserAsync("Laura"); // different organizer
        var carol = await SeedUserAsync("Carol");

        var marcoNight = await SeedGameNightAsync(marco.Id);
        var lauraNight = await SeedGameNightAsync(laura.Id);
        await SeedRsvpAsync(marcoNight.Id, carol.Id);
        await SeedRsvpAsync(lauraNight.Id, carol.Id); // should NOT count for marco

        var result = await _sut.Handle(new GetRegularsQuery(marco.Id), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(carol.Id);
        result[0].EventCount.Should().Be(1);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Limit
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RespectsLimit()
    {
        var organizer = await SeedUserAsync();
        for (var i = 0; i < 15; i++)
        {
            var u = await SeedUserAsync($"User{i}");
            var gn = await SeedGameNightAsync(organizer.Id);
            await SeedRsvpAsync(gn.Id, u.Id);
        }

        var result = await _sut.Handle(new GetRegularsQuery(organizer.Id, Limit: 5), CancellationToken.None);

        result.Should().HaveCount(5);
    }

    // ────────────────────────────────────────────────────────────────────────
    // DTO contract
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PopulatesDtoFieldsFromUser()
    {
        var organizer = await SeedUserAsync();
        var alice = await SeedUserAsync(displayName: "Alice", email: "alice@example.com");
        // PR #1294 review fix: LastInvitedAt is sourced from GameNight.ScheduledAt
        // (event-occurrence semantic), not GameNightRsvp.CreatedAt (invitation-creation).
        var gn = await SeedGameNightAsync(organizer.Id, scheduledAt: DateTimeOffset.UtcNow.AddDays(-7));
        await SeedRsvpAsync(gn.Id, alice.Id);

        var result = await _sut.Handle(new GetRegularsQuery(organizer.Id), CancellationToken.None);

        result.Should().ContainSingle();
        var dto = result[0];
        dto.Id.Should().Be(alice.Id);
        dto.DisplayName.Should().Be("Alice");
        dto.Email.Should().Be("alice@example.com");
        dto.EventCount.Should().Be(1);
        dto.LastInvitedAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(-7), TimeSpan.FromMinutes(1));
    }
}
