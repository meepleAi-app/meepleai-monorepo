using System.Runtime.CompilerServices;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="LiveSessionStreamGateway"/>.
/// Verifies companion-id resolution and delegation to <see cref="ISessionBroadcastService"/>.
/// Issue #2561 SP2 T3.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2561")]
public sealed class LiveSessionStreamGatewayTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reconstitutes a <see cref="LiveGameSession"/> with the given <paramref name="trackingSessionId"/>
    /// using the internal <c>Reconstitute</c> factory (InternalsVisibleTo: Api.Tests).
    /// Minimal arguments — collections are empty, enums default.
    /// </summary>
    private static LiveGameSession FakeLiveSessionWith(Guid? trackingSessionId)
    {
        return LiveGameSession.Reconstitute(
            id: Guid.NewGuid(),
            sessionCode: "ABC123",
            gameId: Guid.NewGuid(),
            gameName: "Test Game",
            toolkitId: null,
            createdByUserId: Guid.NewGuid(),
            visibility: PlayRecordVisibility.Private,
            groupId: null,
            status: LiveSessionStatus.InProgress,
            createdAt: DateTime.UtcNow,
            startedAt: DateTime.UtcNow,
            pausedAt: null,
            completedAt: null,
            updatedAt: DateTime.UtcNow,
            lastSavedAt: null,
            currentTurnIndex: 0,
            currentPhaseIndex: 0,
            phaseNames: Array.Empty<string>(),
            snapshotTriggerConfig: null,
            lastSnapshotTimestamp: null,
            scoringConfig: SessionScoringConfig.CreateDefault(),
            gameState: null,
            notes: null,
            agentMode: AgentSessionMode.None,
            turnAdvancePolicy: TurnAdvancePolicy.Manual,
            trackingSessionId: trackingSessionId,
            xmin: 0,
            players: Enumerable.Empty<LiveSessionPlayer>(),
            teams: Enumerable.Empty<LiveSessionTeam>(),
            turnOrder: Enumerable.Empty<Guid>(),
            roundScores: Enumerable.Empty<RoundScore>(),
            turnRecords: Enumerable.Empty<TurnRecord>(),
            disputes: Enumerable.Empty<RuleDisputeEntry>(),
            diaryEntries: Enumerable.Empty<DiaryEntry>(),
            setupChecklist: null);
    }

    // ── BroadcastAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task BroadcastAsync_resolves_companion_and_publishes()
    {
        // Arrange
        var liveId = Guid.NewGuid();
        var companionId = Guid.NewGuid();

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: companionId));

        var broadcast = new Mock<ISessionBroadcastService>();

        var sut = new LiveSessionStreamGateway(
            repo.Object,
            broadcast.Object,
            NullLogger<LiveSessionStreamGateway>.Instance);

        var evt = new LiveSessionStreamEvent("session:score", new { value = 3 });

        // Act
        await sut.BroadcastAsync(liveId, evt);

        // Assert
        broadcast.Verify(
            b => b.PublishEnvelopeAsync(
                companionId,
                It.Is<SseEventEnvelope>(e => e.EventType == "session:score"),
                It.IsAny<EventVisibility>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastAsync_noop_when_no_companion()
    {
        // Arrange
        var liveId = Guid.NewGuid();

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: null));

        var broadcast = new Mock<ISessionBroadcastService>();

        var sut = new LiveSessionStreamGateway(
            repo.Object,
            broadcast.Object,
            NullLogger<LiveSessionStreamGateway>.Instance);

        var evt = new LiveSessionStreamEvent("session:score", new { value = 3 });

        // Act
        await sut.BroadcastAsync(liveId, evt);

        // Assert
        broadcast.Verify(
            b => b.PublishEnvelopeAsync(
                It.IsAny<Guid>(),
                It.IsAny<SseEventEnvelope>(),
                It.IsAny<EventVisibility>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BroadcastAsync_noop_when_session_not_found()
    {
        // Arrange
        var liveId = Guid.NewGuid();

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var broadcast = new Mock<ISessionBroadcastService>();

        var sut = new LiveSessionStreamGateway(
            repo.Object,
            broadcast.Object,
            NullLogger<LiveSessionStreamGateway>.Instance);

        // Act
        await sut.BroadcastAsync(liveId, new LiveSessionStreamEvent("session:turn", new { }));

        // Assert — no publish if session is missing
        broadcast.Verify(
            b => b.PublishEnvelopeAsync(
                It.IsAny<Guid>(),
                It.IsAny<SseEventEnvelope>(),
                It.IsAny<EventVisibility>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── SubscribeAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SubscribeAsync_returns_empty_stream_when_no_companion()
    {
        // Arrange
        var liveId = Guid.NewGuid();

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: null));

        var broadcast = new Mock<ISessionBroadcastService>();

        var sut = new LiveSessionStreamGateway(
            repo.Object,
            broadcast.Object,
            NullLogger<LiveSessionStreamGateway>.Instance);

        // Act
        var events = new List<LiveSessionStreamEvent>();
        await foreach (var e in sut.SubscribeAsync(liveId, Guid.NewGuid(), null, CancellationToken.None))
            events.Add(e);

        // Assert
        Assert.Empty(events);
        broadcast.Verify(
            b => b.SubscribeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubscribeAsync_maps_envelopes_to_events_with_id()
    {
        // Arrange
        var liveId = Guid.NewGuid();
        var companionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var envelope = new SseEventEnvelope
        {
            Id = "evt-42",
            EventType = "session:score",
            Data = new { value = 5 }
        };

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: companionId));

        var broadcast = new Mock<ISessionBroadcastService>();
        broadcast
            .Setup(b => b.SubscribeAsync(companionId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(SingleItemAsyncEnumerable(envelope));

        var sut = new LiveSessionStreamGateway(
            repo.Object,
            broadcast.Object,
            NullLogger<LiveSessionStreamGateway>.Instance);

        // Act
        var events = new List<LiveSessionStreamEvent>();
        await foreach (var e in sut.SubscribeAsync(liveId, userId, null, CancellationToken.None))
            events.Add(e);

        // Assert
        Assert.Single(events);
        Assert.Equal("session:score", events[0].Type);
        Assert.Equal("evt-42", events[0].Id);
    }

    // ── helper ───────────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<SseEventEnvelope> SingleItemAsyncEnumerable(
        SseEventEnvelope item,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield return item;
    }
}
