using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="LiveSessionStreamGateway"/>.
/// Verifies companion-id resolution, 3-way fan-in merge, and delegation to
/// <see cref="ISessionBroadcastService"/> / <see cref="ISessionSyncService"/>.
/// Issue #2561 SP2 T3 / Issue #2579 fan-in.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2579")]
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

    /// <summary>Creates a gateway SUT with default empty mocks for ISessionSyncService.</summary>
    private static LiveSessionStreamGateway BuildSut(
        ILiveSessionRepository repo,
        ISessionBroadcastService broadcast,
        ISessionSyncService? syncService = null)
    {
        ISessionSyncService sync;
        if (syncService is not null)
        {
            sync = syncService;
        }
        else
        {
            var syncMock = new Mock<ISessionSyncService>();
            syncMock
                .Setup(s => s.SubscribeToSessionEvents(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(EmptyNotificationAsyncEnumerable());
            sync = syncMock.Object;
        }

        return new LiveSessionStreamGateway(
            repo,
            broadcast,
            sync,
            NullLogger<LiveSessionStreamGateway>.Instance);
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

        var sut = BuildSut(repo.Object, broadcast.Object);

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

        var sut = BuildSut(repo.Object, broadcast.Object);

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

        var sut = BuildSut(repo.Object, broadcast.Object);

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

    // ── SubscribeAsync — companion path (existing, unchanged semantics) ────────

    [Fact]
    public async Task SubscribeAsync_companion_envelope_reaches_subscriber_with_original_id()
    {
        // Requirement (c): companion-channel envelope preserves its original Id.
        // Arrange
        var liveId = Guid.NewGuid();
        var companionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var companionEnvelope = new SseEventEnvelope
        {
            Id = "evt-42",
            EventType = "session:score",
            Data = new { value = 5 }
        };

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: companionId));

        var broadcast = new Mock<ISessionBroadcastService>();
        // Companion channel returns one event with Id preserved.
        broadcast
            .Setup(b => b.SubscribeAsync(companionId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(SingleEnvelopeAsyncEnumerable(companionEnvelope));
        // Toolkit channel (liveId) returns empty.
        broadcast
            .Setup(b => b.SubscribeAsync(liveId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(EmptyEnvelopeAsyncEnumerable());

        var sync = new Mock<ISessionSyncService>();
        sync
            .Setup(s => s.SubscribeToSessionEvents(liveId, It.IsAny<CancellationToken>()))
            .Returns(EmptyNotificationAsyncEnumerable());

        var sut = BuildSut(repo.Object, broadcast.Object, sync.Object);

        // Act
        var events = new List<LiveSessionStreamEvent>();
        await foreach (var e in sut.SubscribeAsync(liveId, userId, null, CancellationToken.None))
            events.Add(e);

        // Assert — companion event arrives with its Id intact
        var scoreEvt = events.FirstOrDefault(e => e.Type == "session:score");
        Assert.NotNull(scoreEvt);
        Assert.Equal("evt-42", scoreEvt.Id);
    }

    [Fact]
    public async Task SubscribeAsync_timer_event_on_companion_channel_reaches_subscriber()
    {
        // #2579: timer events (Start/Pause/Resume/Reset) are published by the SessionTracking
        // RandomTool handlers via ISessionBroadcastService.PublishAsync(SessionId, ...), where
        // SessionId is the SessionTracking session id == the companion's TrackingSessionId. The
        // native stream therefore carries them via the COMPANION pump (keyed on TrackingSessionId),
        // NOT the SBS toolkit pump (keyed on liveSessionId). This guards that "session:timer"
        // reaches the native /live-sessions/{id}/stream surface.
        // Arrange
        var liveId = Guid.NewGuid();
        var companionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var timerEnvelope = new SseEventEnvelope
        {
            Id = "timer-evt-1",
            EventType = "session:timer",
            Data = new { durationSeconds = 60 }
        };

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: companionId));

        var broadcast = new Mock<ISessionBroadcastService>();
        // Timer arrives on the companion channel (TrackingSessionId) → Pump 1.
        broadcast
            .Setup(b => b.SubscribeAsync(companionId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(SingleEnvelopeAsyncEnumerable(timerEnvelope));
        // Toolkit channel (liveId) is empty — the timer does NOT come through here.
        broadcast
            .Setup(b => b.SubscribeAsync(liveId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(EmptyEnvelopeAsyncEnumerable());

        var sut = BuildSut(repo.Object, broadcast.Object);

        // Act
        var events = new List<LiveSessionStreamEvent>();
        await foreach (var e in sut.SubscribeAsync(liveId, userId, null, CancellationToken.None))
            events.Add(e);

        // Assert — the timer event reaches the native stream with its type intact
        var timerEvt = events.FirstOrDefault(e => e.Type == "session:timer");
        Assert.NotNull(timerEvt);
        Assert.Equal("timer-evt-1", timerEvt.Id); // companion pump preserves the Id
    }

    // ── SubscribeAsync — SBS toolkit source (a) ───────────────────────────────

    [Fact]
    public async Task SubscribeAsync_sbs_toolkit_event_arrives_with_null_id()
    {
        // Requirement (a): SBS event on liveSessionId channel reaches subscriber with Id == null.
        // Arrange
        var liveId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var toolkitEnvelope = new SseEventEnvelope
        {
            Id = "should-be-dropped",
            EventType = "session:toolkit",
            Data = new { turn = 2 }
        };

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: null)); // no companion

        var broadcast = new Mock<ISessionBroadcastService>();
        // Toolkit channel returns one event.
        broadcast
            .Setup(b => b.SubscribeAsync(liveId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(SingleEnvelopeAsyncEnumerable(toolkitEnvelope));

        var sync = new Mock<ISessionSyncService>();
        sync
            .Setup(s => s.SubscribeToSessionEvents(liveId, It.IsAny<CancellationToken>()))
            .Returns(EmptyNotificationAsyncEnumerable());

        var sut = BuildSut(repo.Object, broadcast.Object, sync.Object);

        // Act
        var events = new List<LiveSessionStreamEvent>();
        await foreach (var e in sut.SubscribeAsync(liveId, userId, null, CancellationToken.None))
            events.Add(e);

        // Assert
        var toolkitEvt = events.FirstOrDefault(e => e.Type == "session:toolkit");
        Assert.NotNull(toolkitEvt);
        Assert.Null(toolkitEvt.Id); // Id must be dropped (null)
    }

    // ── SubscribeAsync — SSS widget source (b) ────────────────────────────────

    [Fact]
    public async Task SubscribeAsync_sss_widget_event_arrives_with_mapped_type_and_null_id()
    {
        // Requirement (b): INotification from SSS arrives as Type == SseEventTypeMapper.GetEventType(evt),
        //                  Data == evt, Id == null.
        // Arrange
        var liveId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var widgetEvt = new WidgetStateUpdatedEvent(
            SessionId: liveId,
            ToolkitId: Guid.NewGuid(),
            WidgetType: "counter",
            StateJson: "{}",
            UpdatedByUserId: userId,
            Timestamp: DateTime.UtcNow);

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: null)); // no companion

        var broadcast = new Mock<ISessionBroadcastService>();
        broadcast
            .Setup(b => b.SubscribeAsync(liveId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(EmptyEnvelopeAsyncEnumerable());

        var sync = new Mock<ISessionSyncService>();
        sync
            .Setup(s => s.SubscribeToSessionEvents(liveId, It.IsAny<CancellationToken>()))
            .Returns(SingleNotificationAsyncEnumerable(widgetEvt));

        var sut = BuildSut(repo.Object, broadcast.Object, sync.Object);

        // Act
        var events = new List<LiveSessionStreamEvent>();
        await foreach (var e in sut.SubscribeAsync(liveId, userId, null, CancellationToken.None))
            events.Add(e);

        // Assert
        Assert.Single(events);
        var mapped = events[0];
        Assert.Equal(SseEventTypeMapper.GetEventType(widgetEvt), mapped.Type); // "session:toolkit"
        Assert.Equal(widgetEvt, mapped.Data);
        Assert.Null(mapped.Id);
    }

    // ── SubscribeAsync — null companion still delivers sources (d) ────────────

    [Fact]
    public async Task SubscribeAsync_when_no_companion_toolkit_and_widget_events_still_deliver()
    {
        // Requirement (d): when TrackingSessionId == null, sources (a)+(b) STILL deliver;
        //                  companion source is simply skipped.
        // Arrange
        var liveId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var toolkitEnvelope = new SseEventEnvelope
        {
            Id = "any",
            EventType = "session:timer",
            Data = new { remaining = 30 }
        };

        var widgetEvt = new WidgetStateUpdatedEvent(
            SessionId: liveId,
            ToolkitId: Guid.NewGuid(),
            WidgetType: "timer",
            StateJson: "{}",
            UpdatedByUserId: userId,
            Timestamp: DateTime.UtcNow);

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: null)); // no companion

        var broadcast = new Mock<ISessionBroadcastService>();
        broadcast
            .Setup(b => b.SubscribeAsync(liveId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(SingleEnvelopeAsyncEnumerable(toolkitEnvelope));

        var sync = new Mock<ISessionSyncService>();
        sync
            .Setup(s => s.SubscribeToSessionEvents(liveId, It.IsAny<CancellationToken>()))
            .Returns(SingleNotificationAsyncEnumerable(widgetEvt));

        var sut = BuildSut(repo.Object, broadcast.Object, sync.Object);

        // Act
        var events = new List<LiveSessionStreamEvent>();
        await foreach (var e in sut.SubscribeAsync(liveId, userId, null, CancellationToken.None))
            events.Add(e);

        // Assert — both sources delivered, companion channel was NOT subscribed
        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.Type == "session:timer" && e.Id == null);
        Assert.Contains(events, e => e.Type == "session:toolkit" && widgetEvt.Equals(e.Data) && e.Id == null);

        // Verify companion channel was never subscribed (no broadcast for a Guid != liveId)
        broadcast.Verify(
            b => b.SubscribeAsync(
                It.Is<Guid>(g => g != liveId),
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── SubscribeAsync — backward-compat: companion maps existing test ─────────

    [Fact]
    public async Task SubscribeAsync_maps_envelopes_to_events_with_id()
    {
        // Existing test preserved: companion path — original Id must be kept.
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
            .Returns(SingleEnvelopeAsyncEnumerable(envelope));
        broadcast
            .Setup(b => b.SubscribeAsync(liveId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(EmptyEnvelopeAsyncEnumerable());

        var sync = new Mock<ISessionSyncService>();
        sync
            .Setup(s => s.SubscribeToSessionEvents(liveId, It.IsAny<CancellationToken>()))
            .Returns(EmptyNotificationAsyncEnumerable());

        var sut = BuildSut(repo.Object, broadcast.Object, sync.Object);

        // Act
        var events = new List<LiveSessionStreamEvent>();
        await foreach (var e in sut.SubscribeAsync(liveId, userId, null, CancellationToken.None))
            events.Add(e);

        // Assert
        var scoreEvt = events.FirstOrDefault(e => e.Type == "session:score");
        Assert.NotNull(scoreEvt);
        Assert.Equal("session:score", scoreEvt.Type);
        Assert.Equal("evt-42", scoreEvt.Id);
    }

    // ── SubscribeAsync — Task 2 regression tests ──────────────────────────────

    /// <summary>
    /// T2-R1: Cancellation teardown — cancel the caller ct while sources never complete;
    /// the returned enumerable must complete and all three underlying subscriptions
    /// must observe cancellation (verified via fake CancellationToken observation).
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_cancellation_tears_down_all_three_pumps()
    {
        // Arrange
        var liveId = Guid.NewGuid();
        var companionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var cts = new CancellationTokenSource();

        // Track which tokens were observed as cancelled by each source
        CancellationToken capturedCompanionToken = default;
        CancellationToken capturedSbsToolkitToken = default;

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: companionId));

        var broadcast = new Mock<ISessionBroadcastService>();

        // Companion: infinite stream that captures the CancellationToken
        broadcast
            .Setup(b => b.SubscribeAsync(companionId, userId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid _, string? _, CancellationToken ct) =>
            {
                capturedCompanionToken = ct;
                return InfiniteEnvelopeAsyncEnumerable(ct);
            });

        // SBS toolkit: infinite stream that captures the CancellationToken
        broadcast
            .Setup(b => b.SubscribeAsync(liveId, userId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid _, string? _, CancellationToken ct) =>
            {
                capturedSbsToolkitToken = ct;
                return InfiniteEnvelopeAsyncEnumerable(ct);
            });

        var syncFake = new CancellationCapturingSyncService(liveId);
        var sut = BuildSut(repo.Object, broadcast.Object, syncFake);

        // Act — start consuming but cancel quickly
        var enumerateTask = Task.Run(async () =>
        {
            // OperationCanceledException is expected when ct is cancelled mid-stream
            try
            {
                await foreach (var _ in sut.SubscribeAsync(liveId, userId, null, cts.Token))
                {
                    // intentionally consume nothing — cancel will stop us
                }
            }
            catch (OperationCanceledException)
            {
                // Expected: the caller's ct was cancelled — enumeration stops.
            }
        });

        // Give the pumps a moment to start, then cancel
        await Task.Delay(20);
        cts.Cancel();

        // The enumeration must complete (not hang) — awaiting this would deadlock if teardown is broken
        await enumerateTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert: all three sources observed cancellation
        Assert.True(capturedCompanionToken.IsCancellationRequested,
            "Companion pump token should be cancelled");
        Assert.True(capturedSbsToolkitToken.IsCancellationRequested,
            "SBS toolkit pump token should be cancelled");
        Assert.True(syncFake.CapturedToken.IsCancellationRequested,
            "SSS widget pump token should be cancelled");
    }

    /// <summary>
    /// T2-R2: No double-delivery — a domain event delivered via the companion source
    /// appears EXACTLY ONCE (it is NOT also on the liveSessionId SBS channel).
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_companion_event_not_double_delivered_via_sbs_channel()
    {
        // Arrange
        var liveId = Guid.NewGuid();
        var companionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var companionEnvelope = new SseEventEnvelope
        {
            Id = "companion-evt-1",
            EventType = "session:score",
            Data = new { value = 10 }
        };

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: companionId));

        var broadcast = new Mock<ISessionBroadcastService>();

        // Companion emits one event
        broadcast
            .Setup(b => b.SubscribeAsync(companionId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(SingleEnvelopeAsyncEnumerable(companionEnvelope));

        // SBS toolkit (liveId) emits NOTHING — domain events are NOT duplicated here
        broadcast
            .Setup(b => b.SubscribeAsync(liveId, userId, null, It.IsAny<CancellationToken>()))
            .Returns(EmptyEnvelopeAsyncEnumerable());

        var sync = new Mock<ISessionSyncService>();
        sync
            .Setup(s => s.SubscribeToSessionEvents(liveId, It.IsAny<CancellationToken>()))
            .Returns(EmptyNotificationAsyncEnumerable());

        var sut = BuildSut(repo.Object, broadcast.Object, sync.Object);

        // Act
        var events = new List<LiveSessionStreamEvent>();
        await foreach (var e in sut.SubscribeAsync(liveId, userId, null, CancellationToken.None))
            events.Add(e);

        // Assert: exactly ONE session:score event — no duplicate
        var scoreEvents = events.Where(e => e.Type == "session:score").ToList();
        Assert.Single(scoreEvents);
        Assert.Equal("companion-evt-1", scoreEvents[0].Id);
    }

    /// <summary>
    /// T2-R3: Replay only to companion — lastEventId is forwarded ONLY to the companion
    /// SubscribeAsync call (Pump 1), NOT to the liveSessionId SBS call (Pump 2).
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_lastEventId_forwarded_only_to_companion_not_to_sbs_toolkit()
    {
        // Arrange
        var liveId = Guid.NewGuid();
        var companionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string lastEventId = "replay-from-42";

        var capturedLastEventIds = new System.Collections.Concurrent.ConcurrentDictionary<Guid, string?>();

        var repo = new Mock<ILiveSessionRepository>();
        repo.Setup(r => r.GetByIdAsync(liveId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeLiveSessionWith(trackingSessionId: companionId));

        var broadcast = new Mock<ISessionBroadcastService>();

        // Capture the lastEventId for companion call
        broadcast
            .Setup(b => b.SubscribeAsync(companionId, userId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, Guid _, string? lei, CancellationToken _) =>
            {
                capturedLastEventIds[id] = lei;
                return EmptyEnvelopeAsyncEnumerable();
            });

        // Capture the lastEventId for liveId (toolkit) call
        broadcast
            .Setup(b => b.SubscribeAsync(liveId, userId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, Guid _, string? lei, CancellationToken _) =>
            {
                capturedLastEventIds[id] = lei;
                return EmptyEnvelopeAsyncEnumerable();
            });

        var sync = new Mock<ISessionSyncService>();
        sync
            .Setup(s => s.SubscribeToSessionEvents(liveId, It.IsAny<CancellationToken>()))
            .Returns(EmptyNotificationAsyncEnumerable());

        var sut = BuildSut(repo.Object, broadcast.Object, sync.Object);

        // Act
        var events = new List<LiveSessionStreamEvent>();
        await foreach (var e in sut.SubscribeAsync(liveId, userId, lastEventId, CancellationToken.None))
            events.Add(e);

        // Assert: companion got lastEventId; SBS toolkit channel got null
        Assert.True(capturedLastEventIds.ContainsKey(companionId),
            "Companion SubscribeAsync should have been called");
        Assert.Equal(lastEventId, capturedLastEventIds[companionId]);

        Assert.True(capturedLastEventIds.ContainsKey(liveId),
            "SBS toolkit SubscribeAsync should have been called");
        Assert.Null(capturedLastEventIds[liveId]);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<SseEventEnvelope> SingleEnvelopeAsyncEnumerable(
        SseEventEnvelope item,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield return item;
    }

    private static async IAsyncEnumerable<SseEventEnvelope> EmptyEnvelopeAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<INotification> SingleNotificationAsyncEnumerable(
        INotification item,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield return item;
    }

    private static async IAsyncEnumerable<INotification> EmptyNotificationAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// Infinite async enumerable for SseEventEnvelope that blocks until the
    /// CancellationToken is triggered — simulates a non-completing SSE subscription.
    /// </summary>
    private static async IAsyncEnumerable<SseEventEnvelope> InfiniteEnvelopeAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            // Wait in small increments; throw on cancellation.
            await Task.Delay(10, ct).ConfigureAwait(false);
        }
        // Propagate so the pump catches OperationCanceledException normally.
        ct.ThrowIfCancellationRequested();
        yield break; // unreachable — keeps compiler happy
    }

    /// <summary>
    /// Fake <see cref="ISessionSyncService"/> that captures the CancellationToken
    /// passed to <see cref="SubscribeToSessionEvents"/> and blocks until it is cancelled.
    /// Used to verify teardown in the cancellation regression test.
    /// </summary>
    private sealed class CancellationCapturingSyncService : ISessionSyncService
    {
        public CancellationToken CapturedToken { get; private set; }

        public CancellationCapturingSyncService(Guid sessionId) { }

        public IAsyncEnumerable<INotification> SubscribeToSessionEvents(
            Guid sessionId,
            CancellationToken ct)
        {
            CapturedToken = ct;
            return InfiniteNotificationAsyncEnumerable(ct);
        }

        public Task PublishEventAsync(Guid sessionId, INotification evt, CancellationToken ct)
            => Task.CompletedTask;

        private static async IAsyncEnumerable<INotification> InfiniteNotificationAsyncEnumerable(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(10, ct).ConfigureAwait(false);
            }
            ct.ThrowIfCancellationRequested();
            yield break; // unreachable — keeps compiler happy
        }
    }
}
