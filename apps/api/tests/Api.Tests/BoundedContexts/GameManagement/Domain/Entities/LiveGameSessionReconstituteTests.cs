using System;
using System.Collections.Generic;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

public class LiveGameSessionReconstituteTests
{
    [Fact]
    public void Reconstitute_PopulatesScalarsAndClearsDomainEvents()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = createdAt.AddMinutes(5);
        var scoringConfig = SessionScoringConfig.CreateDefault();

        // Act
        var session = LiveGameSession.Reconstitute(
            id: id,
            sessionCode: "ABC123",
            gameId: null,
            gameName: "Mage Knight",
            toolkitId: null,
            createdByUserId: userId,
            visibility: PlayRecordVisibility.Private,
            groupId: null,
            status: LiveSessionStatus.InProgress,
            createdAt: createdAt,
            startedAt: createdAt.AddSeconds(30),
            pausedAt: null,
            completedAt: null,
            updatedAt: updatedAt,
            lastSavedAt: null,
            currentTurnIndex: 3,
            currentPhaseIndex: 1,
            phaseNames: new[] { "Setup", "Action", "End" },
            snapshotTriggerConfig: null,
            lastSnapshotTimestamp: null,
            scoringConfig: scoringConfig,
            gameState: null,
            notes: "first turn ok",
            agentMode: AgentSessionMode.None,
            chatSessionId: null,
            turnAdvancePolicy: TurnAdvancePolicy.Manual,
            trackingSessionId: null,
            xmin: 42u,
            players: Array.Empty<LiveSessionPlayer>(),
            teams: Array.Empty<LiveSessionTeam>(),
            turnOrder: Array.Empty<Guid>(),
            roundScores: Array.Empty<RoundScore>(),
            turnRecords: Array.Empty<TurnRecord>(),
            disputes: Array.Empty<RuleDisputeEntry>(),
            setupChecklist: null);

        // Assert
        session.Id.Should().Be(id);
        session.SessionCode.Should().Be("ABC123");
        session.Status.Should().Be(LiveSessionStatus.InProgress);
        session.CurrentTurnIndex.Should().Be(3);
        session.CurrentPhaseIndex.Should().Be(1);
        session.PhaseNames.Should().BeEquivalentTo(new[] { "Setup", "Action", "End" });
        session.Notes.Should().Be("first turn ok");
        session.Xmin.Should().Be(42u);
        session.DomainEvents.Should().BeEmpty("Reconstitute MUST NOT raise events");
    }
}
