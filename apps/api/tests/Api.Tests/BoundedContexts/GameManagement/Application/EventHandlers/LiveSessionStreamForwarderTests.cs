using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="LiveSessionStreamForwarder"/>.
/// Verifies that each mapped domain event is forwarded to <see cref="ILiveSessionStreamGateway"/>
/// with the correct canonical SSE event type (ADR-083 SP2 Task 5).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class LiveSessionStreamForwarderTests
{
    private readonly Mock<ILiveSessionStreamGateway> _gateway;
    private readonly LiveSessionStreamForwarder _handler;

    public LiveSessionStreamForwarderTests()
    {
        _gateway = new Mock<ILiveSessionStreamGateway>();
        _gateway
            .Setup(g => g.BroadcastAsync(It.IsAny<Guid>(), It.IsAny<LiveSessionStreamEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new LiveSessionStreamForwarder(
            _gateway.Object,
            NullLogger<LiveSessionStreamForwarder>.Instance);
    }

    [Fact]
    public async Task Forwards_score_event_as_session_score()
    {
        var sid = Guid.NewGuid();
        var pid = Guid.NewGuid();
        var evt = new LiveSessionScoreRecordedEvent(sid, pid, round: 1, dimension: "vp", value: 5);

        await _handler.Handle(evt, CancellationToken.None);

        _gateway.Verify(g => g.BroadcastAsync(
            sid,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:score"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Forwards_turn_event_as_session_turn()
    {
        var sid = Guid.NewGuid();
        var pid = Guid.NewGuid();
        var evt = new LiveSessionTurnAdvancedEvent(sid, newTurnIndex: 2, currentPlayerId: pid);

        await _handler.Handle(evt, CancellationToken.None);

        _gateway.Verify(g => g.BroadcastAsync(
            sid,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:turn"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Forwards_phase_event_as_session_phase()
    {
        var sid = Guid.NewGuid();
        var evt = new LiveSessionPhaseAdvancedEvent(sid, turnIndex: 1, newPhaseIndex: 2, phaseName: "Combat", totalPhases: 3);

        await _handler.Handle(evt, CancellationToken.None);

        _gateway.Verify(g => g.BroadcastAsync(
            sid,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:phase"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Forwards_player_added_event_as_session_player_join()
    {
        var sid = Guid.NewGuid();
        var pid = Guid.NewGuid();
        var evt = new LiveSessionPlayerAddedEvent(sid, pid, userId: null, displayName: "Alice", role: PlayerRole.Player);

        await _handler.Handle(evt, CancellationToken.None);

        _gateway.Verify(g => g.BroadcastAsync(
            sid,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:player-join"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Forwards_player_removed_event_as_session_player_leave()
    {
        var sid = Guid.NewGuid();
        var pid = Guid.NewGuid();
        var evt = new LiveSessionPlayerRemovedEvent(sid, pid);

        await _handler.Handle(evt, CancellationToken.None);

        _gateway.Verify(g => g.BroadcastAsync(
            sid,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:player-leave"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Forwards_paused_event_as_session_pause()
    {
        var sid = Guid.NewGuid();
        var evt = new LiveSessionPausedEvent(sid, pausedAt: DateTime.UtcNow);

        await _handler.Handle(evt, CancellationToken.None);

        _gateway.Verify(g => g.BroadcastAsync(
            sid,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:pause"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Forwards_resumed_event_as_session_resume()
    {
        var sid = Guid.NewGuid();
        var evt = new LiveSessionResumedEvent(sid, resumedAt: DateTime.UtcNow);

        await _handler.Handle(evt, CancellationToken.None);

        _gateway.Verify(g => g.BroadcastAsync(
            sid,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:resume"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Forwards_completed_event_as_session_endgame()
    {
        var sid = Guid.NewGuid();
        var uid = Guid.NewGuid();
        var players = new List<CompletedPlayerSnapshot>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Alice", 10, 1)
        };
        var scores = new List<CompletedScoreSnapshot>
        {
            new(players[0].PlayerId, "vp", 10, null)
        };
        var evt = new LiveSessionCompletedEvent(
            sid, DateTime.UtcNow, totalTurns: 3,
            gameId: null, gameName: "Catan", createdByUserId: uid,
            visibility: PlayRecordVisibility.Private, groupId: null,
            sessionDate: DateTime.UtcNow.AddHours(-1), startedAt: DateTime.UtcNow.AddMinutes(-30),
            notes: null, players: players, scores: scores);

        await _handler.Handle(evt, CancellationToken.None);

        _gateway.Verify(g => g.BroadcastAsync(
            sid,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:endgame"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Forwards_diary_entry_added_event_as_session_diary()
    {
        // Arrange
        var sid = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.Zero);
        var evt = new LiveSessionDiaryEntryAddedEvent(sid, entryId, authorId, "Great move!", createdAt);

        LiveSessionStreamEvent? captured = null;
        _gateway
            .Setup(g => g.BroadcastAsync(It.IsAny<Guid>(), It.IsAny<LiveSessionStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, LiveSessionStreamEvent, CancellationToken>((_, e, _) => captured = e)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — event type
        _gateway.Verify(g => g.BroadcastAsync(
            sid,
            It.Is<LiveSessionStreamEvent>(e => e.Type == "session:diary"),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — payload fields via reflection on anonymous object
        captured.Should().NotBeNull();
        var data = captured!.Data;
        var dataType = data.GetType();

        dataType.GetProperty("entryId")!.GetValue(data).Should().Be(entryId);
        dataType.GetProperty("authorId")!.GetValue(data).Should().Be(authorId);
        dataType.GetProperty("content")!.GetValue(data).Should().Be("Great move!");
        dataType.GetProperty("timestamp")!.GetValue(data).Should().Be(createdAt.ToString("o"));
    }
}
