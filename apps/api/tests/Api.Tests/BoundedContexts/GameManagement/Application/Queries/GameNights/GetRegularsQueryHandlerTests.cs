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

    private async Task<GameNightEventEntity> SeedGameNightAsync(Guid organizerId, DateTimeOffset? createdAt = null)
    {
        var gn = new GameNightEventEntity
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Test Night",
            ScheduledAt = (createdAt ?? DateTimeOffset.UtcNow).AddDays(7),
            GameIdsJson = "[]",
            Status = "Draft",
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        };
        _dbContext.GameNightEvents.Add(gn);
        await _dbContext.SaveChangesAsync();
        return gn;
    }

    private async Task SeedRsvpAsync(Guid eventId, Guid userId, DateTimeOffset? createdAt = null)
    {
        var rsvp = new GameNightRsvpEntity
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = "Pending",
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
    // 12-month window
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExcludesRsvpsOlderThan12Months()
    {
        var organizer = await SeedUserAsync();
        var old = await SeedUserAsync("OldFriend");
        var recent = await SeedUserAsync("RecentFriend");
        var gn1 = await SeedGameNightAsync(organizer.Id);
        var gn2 = await SeedGameNightAsync(organizer.Id);

        await SeedRsvpAsync(gn1.Id, old.Id, DateTimeOffset.UtcNow.AddMonths(-13));
        await SeedRsvpAsync(gn2.Id, recent.Id, DateTimeOffset.UtcNow.AddMonths(-1));

        var result = await _sut.Handle(new GetRegularsQuery(organizer.Id), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(recent.Id);
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
        var gn = await SeedGameNightAsync(organizer.Id);
        await SeedRsvpAsync(gn.Id, alice.Id, DateTimeOffset.UtcNow.AddDays(-7));

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
