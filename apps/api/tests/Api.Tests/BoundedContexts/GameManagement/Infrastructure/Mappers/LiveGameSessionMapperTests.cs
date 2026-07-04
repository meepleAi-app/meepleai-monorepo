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
    public void ToEntity_PreservesXmin_ForOptimisticConcurrency()
    {
        var session = LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            gameName: "X");

        var entity = LiveGameSessionMapper.ToEntity(session);

        // Xmin is server-owned (Postgres assigns on INSERT/UPDATE). For a fresh domain
        // aggregate the value is 0; EF will overwrite it after SaveChangesAsync. The
        // contract here is that the mapper round-trips whatever value the domain currently
        // holds — not that the value is non-zero at this point.
        entity.Xmin.Should().Be(session.Xmin);
    }

    [Fact]
    public void Mapper_RoundTrips_TrackingSessionId()
    {
        var trackingId = Guid.NewGuid();
        var domain = LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            gameName: "Mage Knight",
            timeProvider: TimeProvider.System,
            gameId: Guid.NewGuid(),
            trackingSessionId: trackingId);

        var entity = LiveGameSessionMapper.ToEntity(domain);
        entity.TrackingSessionId.Should().Be(trackingId);

        var back = LiveGameSessionMapper.ToDomain(entity);
        back.TrackingSessionId.Should().Be(trackingId);
    }

    [Fact]
    public void Mapper_RoundTrips_NullTrackingSessionId()
    {
        // #2552: explicit null round-trip — a free-form session (no GameId companion) must keep
        // TrackingSessionId null through ToEntity → ToDomain, never coalescing to Guid.Empty.
        var domain = LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            gameName: "Mage Knight",
            timeProvider: TimeProvider.System,
            gameId: null,
            trackingSessionId: null);

        var entity = LiveGameSessionMapper.ToEntity(domain);
        entity.TrackingSessionId.Should().BeNull();

        var back = LiveGameSessionMapper.ToDomain(entity);
        back.TrackingSessionId.Should().BeNull();
    }

    // ── CorrelatedGameSessionId round-trips (#2587 Slice 1 Task 1) ──────────

    [Fact]
    public void Mapper_RoundTrips_CorrelatedGameSessionId()
    {
        // #2587: CorrelatedGameSessionId must survive ToEntity → ToDomain round-trip.
        var correlatedId = Guid.NewGuid();
        var domain = LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            gameName: "Mage Knight",
            timeProvider: TimeProvider.System,
            gameId: Guid.NewGuid(),
            correlatedGameSessionId: correlatedId);

        var entity = LiveGameSessionMapper.ToEntity(domain);
        entity.CorrelatedGameSessionId.Should().Be(correlatedId);

        var back = LiveGameSessionMapper.ToDomain(entity);
        back.CorrelatedGameSessionId.Should().Be(correlatedId);
    }

    [Fact]
    public void Mapper_RoundTrips_NullCorrelatedGameSessionId()
    {
        // #2587: null CorrelatedGameSessionId (free-form / pre-Slice1 session) must remain null.
        var domain = LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            gameName: "Mage Knight",
            timeProvider: TimeProvider.System,
            gameId: null,
            correlatedGameSessionId: null);

        var entity = LiveGameSessionMapper.ToEntity(domain);
        entity.CorrelatedGameSessionId.Should().BeNull();

        var back = LiveGameSessionMapper.ToDomain(entity);
        back.CorrelatedGameSessionId.Should().BeNull();
    }
}
