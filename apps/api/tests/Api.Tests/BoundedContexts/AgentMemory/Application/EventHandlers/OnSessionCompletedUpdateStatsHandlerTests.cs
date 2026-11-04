using Api.BoundedContexts.AgentMemory.Application.EventHandlers;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure.DomainEventOutbox;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
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
        aliceMemory.GameStats.Should().ContainSingle();
        aliceMemory.GameStats[0].GameId.Should().Be(gameId);
        aliceMemory.GameStats[0].Wins.Should().Be(1);
        aliceMemory.GameStats[0].Losses.Should().Be(0);
        aliceMemory.GameStats[0].BestScore.Should().Be(100);

        _playerMemoryRepoMock.Verify(
            r => r.UpdateAsync(aliceMemory, It.IsAny<CancellationToken>()), Times.Once);

        // Assert: Bob created
        capturedGuestMemory.Should().NotBeNull();
        capturedGuestMemory!.GuestName.Should().Be("Bob");
        capturedGuestMemory.GameStats.Should().ContainSingle();
        capturedGuestMemory.GameStats[0].Wins.Should().Be(0);
        capturedGuestMemory.GameStats[0].Losses.Should().Be(1);

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
        group.Stats.TotalSessions.Should().Be(1);
        group.Stats.GamePlayCounts.ContainsKey(gameId).Should().BeTrue();
        group.Stats.GamePlayCounts[gameId].Should().Be(1);
        group.Stats.LastPlayedAt.Should().Be(completedAt);

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

    [Fact]
    public async Task Handle_TheSameCompletionRebuiltByTheOutbox_CountsEachPlayerAndTheGroupOnce()
    {
        // The handler is delivered again when the outbox retries the row, and the event it gets is rebuilt from the
        // JSON of the row. Its guard (CF-3 / #1939) compares EventId, which holds only since #4841 made EventId survive
        // that rebuild. NOT proved here: the guard remembers a single event per player and group (LastProcessedEventId),
        // so a second delivery after ANOTHER event for the same player is counted again. That is what option C of #4839
        // (idempotency on the session) would close, and why option B (rethrow, let the outbox retry) is not in.
        var gameId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var original = CreateNotification(
            gameId,
            groupId: groupId,
            players: [new CompletedPlayerSnapshot(Guid.NewGuid(), userId, "Alice", 50, 1)]);
        var rebuilt = JsonSerializer.Deserialize<LiveSessionCompletedEvent>(
            JsonSerializer.Serialize(original, DomainEventJsonOptions.Default), DomainEventJsonOptions.Default)!;
        rebuilt.Should().NotBeSameAs(original);

        var alice = PlayerMemory.CreateForUser(userId);
        var group = GroupMemory.Create(userId, "Test Group");
        _playerMemoryRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alice);
        _groupMemoryRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await _handler.Handle(original, CancellationToken.None);
        await _handler.Handle(rebuilt, CancellationToken.None);

        alice.GameStats.Should().ContainSingle().Which.Wins.Should().Be(1, "the second delivery is the same event");
        group.Stats.TotalSessions.Should().Be(1);
        group.Stats.GamePlayCounts[gameId].Should().Be(1);
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
