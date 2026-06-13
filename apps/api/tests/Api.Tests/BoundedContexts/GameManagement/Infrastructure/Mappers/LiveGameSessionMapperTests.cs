using System;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence.Mappers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Mappers;

public class LiveGameSessionMapperTests
{
    [Fact]
    public void RoundTrip_NewlyCreatedSession_PreservesScalarsAndCollections()
    {
        // Arrange — start from a Domain aggregate created via factory
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var original = LiveGameSession.Create(
            id: sessionId,
            createdByUserId: userId,
            gameName: "Mage Knight",
            timeProvider: TimeProvider.System,
            gameId: null,
            visibility: PlayRecordVisibility.Private,
            groupId: null,
            scoringConfig: SessionScoringConfig.CreateDefault(),
            agentMode: AgentSessionMode.None,
            turnAdvancePolicy: TurnAdvancePolicy.Manual);
        original.AddPlayer(userId: userId, displayName: "Aaron", color: PlayerColor.Red);
        original.ConfigurePhases(new[] { "Movement", "Action", "End" });

        // Act — round-trip
        var entity = LiveGameSessionMapper.ToEntity(original);
        var roundTripped = LiveGameSessionMapper.ToDomain(entity);

        // Assert — scalars
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.SessionCode.Should().Be(original.SessionCode);
        roundTripped.GameName.Should().Be("Mage Knight");
        roundTripped.Status.Should().Be(LiveSessionStatus.Created);
        roundTripped.PhaseNames.Should().BeEquivalentTo(new[] { "Movement", "Action", "End" });
        roundTripped.CurrentPhaseIndex.Should().Be(0);
        roundTripped.TurnAdvancePolicy.Should().Be(TurnAdvancePolicy.Manual);

        // Assert — child collections
        roundTripped.Players.Should().HaveCount(1);
        roundTripped.Players[0].DisplayName.Should().Be("Aaron");
        roundTripped.Players[0].Role.Should().Be(PlayerRole.Host, "first player is auto-host");
        roundTripped.TurnOrder.Should().ContainSingle(pid => pid == roundTripped.Players[0].Id);

        // Assert — domain events must be cleared after Reconstitute
        roundTripped.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ToEntity_PreservesRowVersion_ForOptimisticConcurrency()
    {
        var session = LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            gameName: "X");

        var entity = LiveGameSessionMapper.ToEntity(session);

        entity.RowVersion.Should().NotBeNull(
            "EF needs a non-null RowVersion to evaluate concurrency token equality on UPDATE");
    }
}
