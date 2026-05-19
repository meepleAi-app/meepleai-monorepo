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
/// Unit tests for <see cref="CheckGameNightConflictQueryHandler"/>.
/// Issue #950 (W1-PR2): /game-nights/check-conflict — spec §7b.3 + §12b BE-4.
/// Verifies the ±2h OVERLAPS window, organizer + invitee scoping, and that
/// terminal-status events (Cancelled, Completed) are excluded.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CheckGameNightConflictQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly CheckGameNightConflictQueryHandler _sut;

    public CheckGameNightConflictQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"CheckConflictTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _sut = new CheckGameNightConflictQueryHandler(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private async Task<UserEntity> SeedUserAsync()
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@example.com",
            DisplayName = "Test",
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
        DateTimeOffset scheduledAt,
        string status = "Published",
        string title = "Existing Night")
    {
        var gn = new GameNightEventEntity
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = title,
            ScheduledAt = scheduledAt,
            GameIdsJson = "[]",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _dbContext.GameNightEvents.Add(gn);
        await _dbContext.SaveChangesAsync();
        return gn;
    }

    private async Task SeedRsvpAsync(Guid eventId, Guid userId)
    {
        _dbContext.GameNightRsvps.Add(new GameNightRsvpEntity
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────────
    // No conflict
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoEvents_ReturnsNoConflict()
    {
        var user = await SeedUserAsync();
        var proposedAt = DateTimeOffset.UtcNow.AddDays(7);

        var result = await _sut.Handle(
            new CheckGameNightConflictQuery(user.Id, proposedAt),
            CancellationToken.None);

        result.HasConflict.Should().BeFalse();
        result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithEventOutsideTwoHourWindow_ReturnsNoConflict()
    {
        var user = await SeedUserAsync();
        var proposedAt = DateTimeOffset.UtcNow.AddDays(7);
        await SeedGameNightAsync(user.Id, proposedAt.AddHours(3));

        var result = await _sut.Handle(
            new CheckGameNightConflictQuery(user.Id, proposedAt),
            CancellationToken.None);

        result.HasConflict.Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Conflict as organizer (BE-4)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithOverlappingEventAsOrganizer_ReturnsConflict()
    {
        var user = await SeedUserAsync();
        var proposedAt = DateTimeOffset.UtcNow.AddDays(7);
        var existing = await SeedGameNightAsync(user.Id, proposedAt.AddMinutes(30), title: "Friday Catan");

        var result = await _sut.Handle(
            new CheckGameNightConflictQuery(user.Id, proposedAt),
            CancellationToken.None);

        result.HasConflict.Should().BeTrue();
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].Id.Should().Be(existing.Id);
        result.Conflicts[0].Title.Should().Be("Friday Catan");
        result.Conflicts[0].Role.Should().Be("organizer");
    }

    [Fact]
    public async Task Handle_AtEdgeOfTwoHourWindow_ReturnsConflict()
    {
        var user = await SeedUserAsync();
        var proposedAt = DateTimeOffset.UtcNow.AddDays(7);

        // Within window (just under +2h)
        await SeedGameNightAsync(user.Id, proposedAt.AddMinutes(119));

        var result = await _sut.Handle(
            new CheckGameNightConflictQuery(user.Id, proposedAt),
            CancellationToken.None);

        result.HasConflict.Should().BeTrue();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Conflict as invitee
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithOverlappingEventAsInvitee_ReturnsConflict()
    {
        var marco = await SeedUserAsync();
        var alice = await SeedUserAsync();
        var proposedAt = DateTimeOffset.UtcNow.AddDays(7);
        var aliceNight = await SeedGameNightAsync(alice.Id, proposedAt.AddHours(1));
        await SeedRsvpAsync(aliceNight.Id, marco.Id);

        var result = await _sut.Handle(
            new CheckGameNightConflictQuery(marco.Id, proposedAt),
            CancellationToken.None);

        result.HasConflict.Should().BeTrue();
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].Role.Should().Be("invitee");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Terminal statuses excluded
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithCancelledEvent_DoesNotReturnConflict()
    {
        var user = await SeedUserAsync();
        var proposedAt = DateTimeOffset.UtcNow.AddDays(7);
        await SeedGameNightAsync(user.Id, proposedAt, status: "Cancelled");

        var result = await _sut.Handle(
            new CheckGameNightConflictQuery(user.Id, proposedAt),
            CancellationToken.None);

        result.HasConflict.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithCompletedEvent_DoesNotReturnConflict()
    {
        var user = await SeedUserAsync();
        var proposedAt = DateTimeOffset.UtcNow.AddDays(7);
        await SeedGameNightAsync(user.Id, proposedAt, status: "Completed");

        var result = await _sut.Handle(
            new CheckGameNightConflictQuery(user.Id, proposedAt),
            CancellationToken.None);

        result.HasConflict.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithDraftEvent_ReturnsConflict()
    {
        // Drafts the organizer has not yet published still occupy their calendar;
        // the wizard should warn about them.
        var user = await SeedUserAsync();
        var proposedAt = DateTimeOffset.UtcNow.AddDays(7);
        await SeedGameNightAsync(user.Id, proposedAt, status: "Draft");

        var result = await _sut.Handle(
            new CheckGameNightConflictQuery(user.Id, proposedAt),
            CancellationToken.None);

        result.HasConflict.Should().BeTrue();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Other organizer's published events that user is NOT invited to are ignored
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithOtherOrganizersEventWithoutInvitation_DoesNotReturnConflict()
    {
        var marco = await SeedUserAsync();
        var laura = await SeedUserAsync(); // unrelated organizer
        var proposedAt = DateTimeOffset.UtcNow.AddDays(7);
        await SeedGameNightAsync(laura.Id, proposedAt);

        var result = await _sut.Handle(
            new CheckGameNightConflictQuery(marco.Id, proposedAt),
            CancellationToken.None);

        result.HasConflict.Should().BeFalse();
    }
}
