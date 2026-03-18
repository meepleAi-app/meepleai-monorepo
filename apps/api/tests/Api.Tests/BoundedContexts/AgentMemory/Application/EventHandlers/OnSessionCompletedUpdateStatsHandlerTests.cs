using Api.BoundedContexts.AgentMemory.Application.EventHandlers;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.EventHandlers;

/// <summary>
/// Tests for OnSessionCompletedUpdateStatsHandler: updates player and group stats on session completion.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class OnSessionCompletedUpdateStatsHandlerTests
{
    private readonly Mock<IPlayerMemoryRepository> _playerMemoryRepoMock;
    private readonly Mock<IGroupMemoryRepository> _groupMemoryRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IFeatureFlagService> _featureFlagsMock;
    private readonly OnSessionCompletedUpdateStatsHandler _handler;

    public OnSessionCompletedUpdateStatsHandlerTests()
    {
        _playerMemoryRepoMock = new Mock<IPlayerMemoryRepository>();
        _groupMemoryRepoMock = new Mock<IGroupMemoryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _featureFlagsMock = new Mock<IFeatureFlagService>();
        var loggerMock = new Mock<ILogger<OnSessionCompletedUpdateStatsHandler>>();

        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(true);

        _handler = new OnSessionCompletedUpdateStatsHandler(
            _playerMemoryRepoMock.Object,
            _groupMemoryRepoMock.Object,
            _unitOfWorkMock.Object,
            _featureFlagsMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithPlayers_UpdatesPlayerStats()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var playerId1 = Guid.NewGuid();
        var playerId2 = Guid.NewGuid();

        var players = new List<CompletedPlayerSnapshot>
        {
            new(playerId1, userId1, "Alice", 100, 1),  // Winner
            new(playerId2, null, "Bob", 80, 2)          // Guest loser
        };

        var notification = CreateNotification(gameId, players: players);

        // Alice has existing memory
        var aliceMemory = PlayerMemory.CreateForUser(userId1);
        _playerMemoryRepoMock
            .Setup(r => r.GetByUserIdAsync(userId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aliceMemory);

        // Bob (guest) has no memory
        _playerMemoryRepoMock
            .Setup(r => r.GetByGuestNameAsync("Bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerMemory?)null);

        PlayerMemory? capturedGuestMemory = null;
        _playerMemoryRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PlayerMemory>(), It.IsAny<CancellationToken>()))
            .Callback<PlayerMemory, CancellationToken>((m, _) => capturedGuestMemory = m)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert: Alice updated
        Assert.Single(aliceMemory.GameStats);
        Assert.Equal(gameId, aliceMemory.GameStats[0].GameId);
        Assert.Equal(1, aliceMemory.GameStats[0].Wins);
        Assert.Equal(0, aliceMemory.GameStats[0].Losses);
        Assert.Equal(100, aliceMemory.GameStats[0].BestScore);

        _playerMemoryRepoMock.Verify(
            r => r.UpdateAsync(aliceMemory, It.IsAny<CancellationToken>()), Times.Once);

        // Assert: Bob created
        Assert.NotNull(capturedGuestMemory);
        Assert.Equal("Bob", capturedGuestMemory!.GuestName);
        Assert.Single(capturedGuestMemory.GameStats);
        Assert.Equal(0, capturedGuestMemory.GameStats[0].Wins);
        Assert.Equal(1, capturedGuestMemory.GameStats[0].Losses);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithGroupId_UpdatesGroupStats()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;

        var players = new List<CompletedPlayerSnapshot>
        {
            new(Guid.NewGuid(), userId, "Alice", 50, 1)
        };

        var notification = CreateNotification(gameId, groupId: groupId, completedAt: completedAt, players: players);

        var group = GroupMemory.Create(userId, "Test Group");
        _groupMemoryRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _playerMemoryRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlayerMemory.CreateForUser(userId));

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        Assert.Equal(1, group.Stats.TotalSessions);
        Assert.True(group.Stats.GamePlayCounts.ContainsKey(gameId));
        Assert.Equal(1, group.Stats.GamePlayCounts[gameId]);
        Assert.Equal(completedAt, group.Stats.LastPlayedAt);

        _groupMemoryRepoMock.Verify(r => r.UpdateAsync(group, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutGroupId_SkipsGroupUpdate()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var players = new List<CompletedPlayerSnapshot>
        {
            new(Guid.NewGuid(), userId, "Alice", 50, 1)
        };

        var notification = CreateNotification(gameId, groupId: null, players: players);

        _playerMemoryRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlayerMemory.CreateForUser(userId));

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _groupMemoryRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FeatureDisabled_DoesNothing()
    {
        // Arrange
        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(false);

        var notification = CreateNotification(Guid.NewGuid());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _playerMemoryRepoMock.Verify(
            r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoGameId_DoesNothing()
    {
        // Arrange
        var notification = CreateNotification(gameId: null);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _playerMemoryRepoMock.Verify(
            r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static LiveSessionCompletedEvent CreateNotification(
        Guid? gameId,
        Guid? groupId = null,
        DateTime? completedAt = null,
        List<CompletedPlayerSnapshot>? players = null)
    {
        return new LiveSessionCompletedEvent(
            sessionId: Guid.NewGuid(),
            completedAt: completedAt ?? DateTime.UtcNow,
            totalTurns: 10,
            gameId: gameId,
            gameName: "Test Game",
            createdByUserId: Guid.NewGuid(),
            visibility: PlayRecordVisibility.Private,
            groupId: groupId,
            sessionDate: DateTime.UtcNow,
            startedAt: DateTime.UtcNow.AddHours(-1),
            notes: null,
            players: players ?? new List<CompletedPlayerSnapshot>(),
            scores: new List<CompletedScoreSnapshot>());
    }
}
