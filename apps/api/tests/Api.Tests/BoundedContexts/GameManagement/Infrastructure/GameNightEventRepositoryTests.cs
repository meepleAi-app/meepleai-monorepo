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
