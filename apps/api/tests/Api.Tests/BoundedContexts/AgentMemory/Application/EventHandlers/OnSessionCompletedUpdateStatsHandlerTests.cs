using Api.BoundedContexts.AgentMemory.Application.EventHandlers;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
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
/// Tests for OnSessionCompletedUpdateStatsHandler: updates the group stats on session completion. The per-player stats
/// were retired by #4876.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class OnSessionCompletedUpdateStatsHandlerTests
{
    private readonly Mock<IGroupMemoryRepository> _groupMemoryRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IFeatureFlagService> _featureFlagsMock;
    private readonly OnSessionCompletedUpdateStatsHandler _handler;

    public OnSessionCompletedUpdateStatsHandlerTests()
    {
        _groupMemoryRepoMock = new Mock<IGroupMemoryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _featureFlagsMock = new Mock<IFeatureFlagService>();
        var loggerMock = new Mock<ILogger<OnSessionCompletedUpdateStatsHandler>>();

        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(true);

        _handler = new OnSessionCompletedUpdateStatsHandler(
            _groupMemoryRepoMock.Object,
            _unitOfWorkMock.Object,
            _featureFlagsMock.Object,
            loggerMock.Object);
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
    public async Task Handle_WithoutGroupId_WritesNothing()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var players = new List<CompletedPlayerSnapshot>
        {
            new(Guid.NewGuid(), userId, "Alice", 50, 1)
        };

        var notification = CreateNotification(Guid.NewGuid(), groupId: null, players: players);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _groupMemoryRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GroupNotFound_WritesNothing()
    {
        var groupId = Guid.NewGuid();
        _groupMemoryRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupMemory?)null);

        await _handler.Handle(CreateNotification(Guid.NewGuid(), groupId: groupId), CancellationToken.None);

        _groupMemoryRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<GroupMemory>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FeatureDisabled_DoesNothing()
    {
        // Arrange
        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(false);

        var notification = CreateNotification(Guid.NewGuid(), groupId: Guid.NewGuid());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _groupMemoryRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoGameId_DoesNothing()
    {
        // Arrange
        var notification = CreateNotification(gameId: null, groupId: Guid.NewGuid());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _groupMemoryRepoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TheSameCompletionRebuiltByTheOutbox_CountsTheGroupOnce()
    {
        // The handler is delivered again when the outbox retries the row, and the event it gets is rebuilt from the
        // JSON of the row. Its guard (CF-3 / #1939) compares EventId, which holds only since #4841 made EventId survive
        // that rebuild. Two consecutive deliveries: the case the single-event guard already covered. The case with
        // ANOTHER event in between is the next test (#4839, option C).
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

        var group = GroupMemory.Create(userId, "Test Group");
        _groupMemoryRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await _handler.Handle(original, CancellationToken.None);
        await _handler.Handle(rebuilt, CancellationToken.None);

        group.Stats.TotalSessions.Should().Be(1, "the second delivery is the same event");
        group.Stats.GamePlayCounts[gameId].Should().Be(1);
    }

    [Fact]
    public async Task Handle_ARedeliveryAfterAnotherSessionOfTheSameGroup_CountsTheSessionOnce()
    {
        // #4839, option C. The outbox commits the batch even when the row of one handler fails, so the statistics
        // written before it stay; the NEXT event of the same group can then be counted before the retry of the first.
        // The old guard remembered only the last event, so the first session was counted again; the set of sessions
        // recognises it whatever came in between.
        var gameId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var firstPlayers = new List<CompletedPlayerSnapshot> { new(Guid.NewGuid(), userId, "Alice", 50, 1) };
        var secondPlayers = new List<CompletedPlayerSnapshot> { new(Guid.NewGuid(), userId, "Alice", 30, 2) };
        var first = CreateNotification(gameId, groupId: groupId, players: firstPlayers);
        var second = CreateNotification(gameId, groupId: groupId, players: secondPlayers);
        var firstRebuilt = JsonSerializer.Deserialize<LiveSessionCompletedEvent>(
            JsonSerializer.Serialize(first, DomainEventJsonOptions.Default), DomainEventJsonOptions.Default)!;

        var group = GroupMemory.Create(userId, "Test Group");
        _groupMemoryRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await _handler.Handle(first, CancellationToken.None);
        await _handler.Handle(second, CancellationToken.None);
        await _handler.Handle(firstRebuilt, CancellationToken.None);

        group.Stats.TotalSessions.Should().Be(2, "two different sessions, the first one delivered twice");
        group.Stats.GamePlayCounts[gameId].Should().Be(2);
    }

    [Fact]
    public async Task Handle_TwoDifferentSessionsOfTheSameGroup_BothAreCounted()
    {
        // The other direction: remembering sessions must not turn the statistics off for a group's next session.
        var gameId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var group = GroupMemory.Create(userId, "Test Group");
        _groupMemoryRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await _handler.Handle(
            CreateNotification(gameId, groupId: groupId, players: [new CompletedPlayerSnapshot(Guid.NewGuid(), userId, "Alice", 50, 1)]),
            CancellationToken.None);
        await _handler.Handle(
            CreateNotification(gameId, groupId: groupId, players: [new CompletedPlayerSnapshot(Guid.NewGuid(), userId, "Alice", 40, 1)]),
            CancellationToken.None);

        group.Stats.TotalSessions.Should().Be(2);
        group.Stats.GamePlayCounts[gameId].Should().Be(2);
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
