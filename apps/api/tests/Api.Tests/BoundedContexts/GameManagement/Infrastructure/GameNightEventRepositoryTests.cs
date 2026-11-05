using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

/// <summary>
/// Unit tests for GameNightEventRepository.MapToDomain enum-parsing safety net.
/// Validates that corrupted persisted status values are mapped to the Corrupted
/// quarantine state with structured error logging instead of throwing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightEventRepositoryTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IDomainEventCollector> _eventCollector;
    private readonly Mock<ILogger<GameNightEventRepository>> _logger;
    private readonly GameNightEventRepository _repository;

    public GameNightEventRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"GameNightRepoTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
        _eventCollector = new Mock<IDomainEventCollector>();
        _logger = new Mock<ILogger<GameNightEventRepository>>();
        _repository = new GameNightEventRepository(_dbContext, _eventCollector.Object, _logger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private GameNightEventEntity SeedRawEntity(
        string status = "Published",
        string rsvpStatus = "Accepted",
        string sessionStatus = "InProgress")
    {
        var eventId = Guid.NewGuid();
        var entity = new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = Guid.NewGuid(),
            Title = "Test Night",
            Description = null,
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(2),
            Location = "Home",
            MaxPlayers = 6,
            GameIdsJson = "[]",
            Status = status,
            Reminder24hSentAt = null,
            Reminder1hSentAt = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        entity.Rsvps.Add(new GameNightRsvpEntity
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = Guid.NewGuid(),
            Status = rsvpStatus,
            RespondedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        entity.Sessions.Add(new GameNightSessionEntity
        {
            Id = Guid.NewGuid(),
            GameNightEventId = eventId,
            SessionId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            GameTitle = "Catan",
            PlayOrder = 1,
            Status = sessionStatus,
            WinnerId = null,
            StartedAt = null,
            CompletedAt = null
        });
        _dbContext.GameNightEvents.Add(entity);
        _dbContext.SaveChanges();
        return entity;
    }

    [Fact]
    public async Task GetByIdAsync_WithValidStatuses_ParsesCorrectly()
    {
        // Arrange
        var entity = SeedRawEntity(status: "Published", rsvpStatus: "Accepted", sessionStatus: "InProgress");

        // Act
        var result = await _repository.GetByIdAsync(entity.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GameNightStatus.Published, result!.Status);
        Assert.Single(result.Rsvps);
        Assert.Equal(RsvpStatus.Accepted, result.Rsvps[0].Status);
        Assert.Single(result.Sessions);
        Assert.Equal(GameNightSessionStatus.InProgress, result.Sessions[0].Status);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidGameNightStatus_FallsBackToCorrupted_AndLogs()
    {
        // Arrange
        var entity = SeedRawEntity(status: "publishedd"); // typo

        // Act
        var result = await _repository.GetByIdAsync(entity.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GameNightStatus.Corrupted, result!.Status);

        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) =>
                v.ToString()!.Contains("Corrupted", StringComparison.Ordinal)
                && v.ToString()!.Contains("publishedd", StringComparison.Ordinal)),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidRsvpStatus_FallsBackToCorrupted_AndLogs()
    {
        // Arrange
        var entity = SeedRawEntity(rsvpStatus: "WRONG_VALUE");

        // Act
        var result = await _repository.GetByIdAsync(entity.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result!.Rsvps);
        Assert.Equal(RsvpStatus.Corrupted, result.Rsvps[0].Status);

        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) =>
                v.ToString()!.Contains("RsvpStatus", StringComparison.Ordinal)
                && v.ToString()!.Contains("WRONG_VALUE", StringComparison.Ordinal)),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidSessionStatus_FallsBackToCorrupted_AndLogs()
    {
        // Arrange
        var entity = SeedRawEntity(sessionStatus: "unknown_state");

        // Act
        var result = await _repository.GetByIdAsync(entity.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result!.Sessions);
        Assert.Equal(GameNightSessionStatus.Corrupted, result.Sessions[0].Status);

        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) =>
                v.ToString()!.Contains("GameNightSessionStatus", StringComparison.Ordinal)
                && v.ToString()!.Contains("unknown_state", StringComparison.Ordinal)),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    // ── #4748: the dashboard lists show the viewer's nights, not everyone's ─────────────────

    private GameNightEventEntity SeedNight(
        Guid organizerId, Guid? invitedUserId, string status, DateTimeOffset scheduledAt)
    {
        var eventId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var entity = new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = organizerId,
            Title = "Night",
            Description = null,
            ScheduledAt = scheduledAt,
            Location = "Home",
            MaxPlayers = 6,
            GameIdsJson = "[]",
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
        if (invitedUserId is { } invited)
        {
            entity.Rsvps.Add(new GameNightRsvpEntity
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                UserId = invited,
                Status = "Accepted",
                RespondedAt = now,
                CreatedAt = now
            });
        }
        _dbContext.GameNightEvents.Add(entity);
        _dbContext.SaveChanges();
        return entity;
    }

    [Fact]
    public async Task GetUpcomingAsync_ReturnsTheNightsTheUserOrganizesOrIsInvitedTo_NotOthers()
    {
        var viewer = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var future = DateTimeOffset.UtcNow.AddDays(2);
        var mine = SeedNight(viewer, null, "Published", future);
        var invitedTo = SeedNight(stranger, viewer, "Published", future);
        SeedNight(stranger, Guid.NewGuid(), "Published", future);

        var result = await _repository.GetUpcomingAsync(viewer, TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { mine.Id, invitedTo.Id }.OrderBy(id => id),
            result.Select(n => n.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetUpcomingAsync_ForAUserWithNoNights_ReturnsNothing()
    {
        var future = DateTimeOffset.UtcNow.AddDays(2);
        SeedNight(Guid.NewGuid(), Guid.NewGuid(), "Published", future);

        var result = await _repository.GetUpcomingAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCompletedAsync_ReturnsTheNightsTheUserOrganizesOrIsInvitedTo_NotOthers()
    {
        var viewer = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var past = DateTimeOffset.UtcNow.AddDays(-3);
        var mine = SeedNight(viewer, null, "Completed", past);
        var invitedTo = SeedNight(stranger, viewer, "Published", past);
        SeedNight(stranger, Guid.NewGuid(), "Completed", past);
        SeedNight(stranger, Guid.NewGuid(), "Published", past);

        var result = await _repository.GetCompletedAsync(viewer, 10, TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { mine.Id, invitedTo.Id }.OrderBy(id => id),
            result.Select(n => n.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetCompletedAsync_ForAUserWithNoNights_ReturnsNothing()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-3);
        SeedNight(Guid.NewGuid(), Guid.NewGuid(), "Completed", past);

        var result = await _repository.GetCompletedAsync(Guid.NewGuid(), 10, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCompletedAsync_StillHonoursTheLimitAndTheStatusForTheUser()
    {
        var viewer = Guid.NewGuid();
        SeedNight(viewer, null, "Completed", DateTimeOffset.UtcNow.AddDays(-5));
        var newest = SeedNight(viewer, null, "Completed", DateTimeOffset.UtcNow.AddDays(-1));
        SeedNight(viewer, null, "Draft", DateTimeOffset.UtcNow.AddDays(-2));
        SeedNight(viewer, null, "Published", DateTimeOffset.UtcNow.AddDays(3));

        var result = await _repository.GetCompletedAsync(viewer, 1, TestContext.Current.CancellationToken);

        Assert.Equal(newest.Id, Assert.Single(result).Id);
    }

    // ── #4759: whoever accepted an email invitation while logged in sits at the table ──────────

    /// <summary>A registered account; the invitations accepted «by email» are addressed to its email.</summary>
    private Guid SeedUser(string? email = null)
    {
        var id = Guid.NewGuid();
        _dbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = id,
            Email = email ?? $"{id:N}@example.com",
            DisplayName = "Viewer",
            PasswordHash = "x",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
        });
        _dbContext.SaveChanges();
        return id;
    }

    private void SeedEmailInvitation(Guid gameNightId, Guid? respondedBy, string status, string? email = null)
    {
        var now = DateTimeOffset.UtcNow;
        _dbContext.GameNightInvitations.Add(new GameNightInvitationEntity
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            GameNightId = gameNightId,
            Email = email ?? (respondedBy is { } user ? $"{user:N}@example.com" : $"{Guid.NewGuid():N}@example.com"),
            Status = status,
            RespondedByUserId = respondedBy,
            RespondedAt = respondedBy is null ? null : now,
            ExpiresAt = now.AddDays(7),
            CreatedAt = now,
            CreatedBy = Guid.NewGuid(),
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetUpcomingAsync_IncludesANightWhoseEmailInvitationTheViewerAccepted()
    {
        // The guest is a registered user who answered the email link while logged in: no RSVP row is
        // ever written for them, and the list used to leave the night out (403 on the detail, too).
        var viewer = SeedUser();
        var future = DateTimeOffset.UtcNow.AddDays(2);
        var acceptedByEmail = SeedNight(Guid.NewGuid(), null, "Published", future);
        SeedEmailInvitation(acceptedByEmail.Id, viewer, "Accepted");

        var result = await _repository.GetUpcomingAsync(viewer, TestContext.Current.CancellationToken);

        Assert.Equal(acceptedByEmail.Id, Assert.Single(result).Id);
    }

    [Theory]
    [InlineData("Declined")]
    [InlineData("Cancelled")]
    [InlineData("Pending")]
    public async Task GetUpcomingAsync_ExcludesANightWhoseEmailInvitationIsNotAccepted(string status)
    {
        // Declined by the viewer, withdrawn by the organizer, or never answered: not a participant.
        var viewer = SeedUser();
        var future = DateTimeOffset.UtcNow.AddDays(2);
        var night = SeedNight(Guid.NewGuid(), null, "Published", future);
        SeedEmailInvitation(night.Id, status == "Pending" ? null : viewer, status);

        var result = await _repository.GetUpcomingAsync(viewer, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUpcomingAsync_AnotherUsersAcceptedEmailInvitation_DoesNotLetTheViewerIn()
    {
        var viewer = SeedUser();
        var night = SeedNight(Guid.NewGuid(), null, "Published", DateTimeOffset.UtcNow.AddDays(2));
        SeedEmailInvitation(night.Id, SeedUser(), "Accepted");

        var result = await _repository.GetUpcomingAsync(viewer, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUpcomingAsync_AForwardedLinkAcceptedByAnotherAccount_DoesNotLetItIn()
    {
        // The decision of 2026-11-05: the link is a credential to answer, not to sit at the table.
        // Alice forwards her link to Bob; Bob accepts while logged in; the invitation is still
        // addressed to Alice, so Bob is not a participant (and stays able to answer, as before).
        var bob = SeedUser();
        var night = SeedNight(Guid.NewGuid(), null, "Published", DateTimeOffset.UtcNow.AddDays(2));
        SeedEmailInvitation(night.Id, bob, "Accepted", email: "alice@example.com");

        var result = await _repository.GetUpcomingAsync(bob, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUpcomingAsync_MatchesTheInvitedAddressTheWayTheInvitationStoresIt()
    {
        // The account's address is normalized like the invitation's (trim, lower case): a user
        // registered as «Anna.Rossi@Example.com» accepting «anna.rossi@example.com» is the same person.
        var anna = SeedUser("Anna.Rossi@Example.com");
        var night = SeedNight(Guid.NewGuid(), null, "Published", DateTimeOffset.UtcNow.AddDays(2));
        SeedEmailInvitation(night.Id, anna, "Accepted", email: "anna.rossi@example.com");

        var result = await _repository.GetUpcomingAsync(anna, TestContext.Current.CancellationToken);

        Assert.Equal(night.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetCompletedAsync_IncludesANightWhoseEmailInvitationTheViewerAccepted()
    {
        var viewer = SeedUser();
        var night = SeedNight(Guid.NewGuid(), null, "Completed", DateTimeOffset.UtcNow.AddDays(-3));
        SeedEmailInvitation(night.Id, viewer, "Accepted");

        var result = await _repository.GetCompletedAsync(viewer, 10, TestContext.Current.CancellationToken);

        Assert.Equal(night.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetByUserAsync_IncludesANightWhoseEmailInvitationTheViewerAccepted()
    {
        var viewer = SeedUser();
        var night = SeedNight(Guid.NewGuid(), null, "Published", DateTimeOffset.UtcNow.AddDays(2));
        SeedEmailInvitation(night.Id, viewer, "Accepted");

        var result = await _repository.GetByUserAsync(viewer, TestContext.Current.CancellationToken);

        Assert.Equal(night.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task HasAcceptedEmailInvitationAsync_IsTrueOnlyForAnAcceptedInvitationAnsweredByThatUser_AtTheirAddress()
    {
        var viewer = SeedUser();
        var night = SeedNight(Guid.NewGuid(), null, "Published", DateTimeOffset.UtcNow.AddDays(2));
        var other = SeedNight(Guid.NewGuid(), null, "Published", DateTimeOffset.UtcNow.AddDays(2));
        var forwarded = SeedNight(Guid.NewGuid(), null, "Published", DateTimeOffset.UtcNow.AddDays(2));
        SeedEmailInvitation(night.Id, viewer, "Accepted");
        SeedEmailInvitation(other.Id, viewer, "Declined");
        SeedEmailInvitation(forwarded.Id, viewer, "Accepted", email: "somebody.else@example.com");

        var ct = TestContext.Current.CancellationToken;
        Assert.True(await _repository.HasAcceptedEmailInvitationAsync(night.Id, viewer, ct));
        Assert.False(await _repository.HasAcceptedEmailInvitationAsync(other.Id, viewer, ct), "declined");
        Assert.False(await _repository.HasAcceptedEmailInvitationAsync(forwarded.Id, viewer, ct), "a forwarded link");
        Assert.False(await _repository.HasAcceptedEmailInvitationAsync(night.Id, SeedUser(), ct), "somebody else");
        Assert.False(await _repository.HasAcceptedEmailInvitationAsync(night.Id, Guid.NewGuid(), ct), "no such account");
        Assert.False(await _repository.HasAcceptedEmailInvitationAsync(night.Id, Guid.Empty, ct), "no caller is nobody");
    }

    [Fact]
    public async Task GetCompletedAsync_AppliesTheUserFilterBeforeTheLimit()
    {
        var viewer = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var mine = SeedNight(viewer, null, "Completed", DateTimeOffset.UtcNow.AddDays(-20));
        SeedNight(stranger, null, "Completed", DateTimeOffset.UtcNow.AddDays(-1));
        SeedNight(stranger, null, "Completed", DateTimeOffset.UtcNow.AddDays(-2));

        var result = await _repository.GetCompletedAsync(viewer, 1, TestContext.Current.CancellationToken);

        Assert.Equal(mine.Id, Assert.Single(result).Id);
    }
}
