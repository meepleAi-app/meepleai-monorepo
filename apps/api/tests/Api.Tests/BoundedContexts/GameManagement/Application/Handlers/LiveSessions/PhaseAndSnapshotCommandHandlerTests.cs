using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

#region AdvanceLiveSessionPhaseCommandHandler

/// <summary>
/// Tests for AdvanceLiveSessionPhaseCommandHandler.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class AdvanceLiveSessionPhaseCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly FakeTimeProvider _timeProvider;

    public AdvanceLiveSessionPhaseCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionRepository>();
        _mediatorMock = new Mock<IMediator>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 19, 14, 0, 0, TimeSpan.Zero));
    }

    private AdvanceLiveSessionPhaseCommandHandler CreateHandler() =>
        new(_repositoryMock.Object, _timeProvider, _mediatorMock.Object);

    private LiveGameSession CreateStartedSessionWithPhases(
        Guid? sessionId = null, string[]? phases = null)
    {
        var session = LiveGameSession.Create(
            sessionId ?? Guid.NewGuid(), Guid.NewGuid(), "Test Game", _timeProvider);
        session.AddPlayer(Guid.NewGuid(), "Player 1", PlayerColor.Red, _timeProvider);
        session.Start(_timeProvider);
        session.ConfigurePhases(phases ?? new[] { "Draw", "Action", "Cleanup" }, _timeProvider);
        return session;
    }

    [Fact]
    public async Task Handle_ValidSession_AdvancesPhaseAndUpdates()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSessionWithPhases(sessionId);
        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        await handler.Handle(new AdvanceLiveSessionPhaseCommand(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal(1, session.CurrentPhaseIndex);
        _repositoryMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new AdvanceLiveSessionPhaseCommand(sessionId), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNull()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithSnapshotTriggerEnabled_TriggersAutoSnapshot()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSessionWithPhases(sessionId);
        var config = SnapshotTriggerConfig.CreateDefault(); // Includes PhaseAdvanced
        session.SetSnapshotTriggerConfig(config, _timeProvider);

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mediatorMock.Setup(m => m.Send(It.IsAny<TriggerEventSnapshotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionSnapshotDto?)null);

        var handler = CreateHandler();
        await handler.Handle(new AdvanceLiveSessionPhaseCommand(sessionId), TestContext.Current.CancellationToken);

        _mediatorMock.Verify(m => m.Send(
            It.Is<TriggerEventSnapshotCommand>(c =>
                c.SessionId == sessionId &&
                c.TriggerType == SnapshotTrigger.PhaseAdvanced),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutSnapshotTriggerConfig_DoesNotTriggerSnapshot()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSessionWithPhases(sessionId);
        // No SnapshotTriggerConfig set

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        await handler.Handle(new AdvanceLiveSessionPhaseCommand(sessionId), TestContext.Current.CancellationToken);

        _mediatorMock.Verify(m => m.Send(
            It.IsAny<TriggerEventSnapshotCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithPhaseAdvancedDisabled_DoesNotTriggerSnapshot()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSessionWithPhases(sessionId);
        // Config without PhaseAdvanced trigger
        var config = new SnapshotTriggerConfig(
            new[] { SnapshotTrigger.TurnAdvanced, SnapshotTrigger.ManualSave });
        session.SetSnapshotTriggerConfig(config, _timeProvider);

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        await handler.Handle(new AdvanceLiveSessionPhaseCommand(sessionId), TestContext.Current.CancellationToken);

        _mediatorMock.Verify(m => m.Send(
            It.IsAny<TriggerEventSnapshotCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSessionWithPhases(sessionId);
        var ct = TestContext.Current.CancellationToken;

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, ct))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        await handler.Handle(new AdvanceLiveSessionPhaseCommand(sessionId), ct);

        _repositoryMock.Verify(r => r.GetByIdAsync(sessionId, ct), Times.Once);
        _repositoryMock.Verify(r => r.UpdateAsync(session, ct), Times.Once);
    }
}

#endregion

#region ConfigureLiveSessionPhasesCommandHandler

/// <summary>
/// Tests for ConfigureLiveSessionPhasesCommandHandler.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class ConfigureLiveSessionPhasesCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repositoryMock;
    private readonly FakeTimeProvider _timeProvider;

    public ConfigureLiveSessionPhasesCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionRepository>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 19, 14, 0, 0, TimeSpan.Zero));
    }

    private ConfigureLiveSessionPhasesCommandHandler CreateHandler() =>
        new(_repositoryMock.Object, _timeProvider);

    private LiveGameSession CreateStartedSession(Guid? sessionId = null)
    {
        var session = LiveGameSession.Create(
            sessionId ?? Guid.NewGuid(), Guid.NewGuid(), "Test Game", _timeProvider);
        session.AddPlayer(Guid.NewGuid(), "Player 1", PlayerColor.Red, _timeProvider);
        session.Start(_timeProvider);
        return session;
    }

    [Fact]
    public async Task Handle_ValidPhases_ConfiguresAndUpdates()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSession(sessionId);
        var phases = new[] { "Draw", "Action", "Cleanup" };

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        await handler.Handle(
            new ConfigureLiveSessionPhasesCommand(sessionId, phases),
            TestContext.Current.CancellationToken);

        Assert.Equal(3, session.PhaseNames.Length);
        Assert.Equal("Draw", session.PhaseNames[0]);
        _repositoryMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new ConfigureLiveSessionPhasesCommand(sessionId, new[] { "Draw" }),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNull()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSession(sessionId);
        var ct = TestContext.Current.CancellationToken;

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, ct))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        await handler.Handle(
            new ConfigureLiveSessionPhasesCommand(sessionId, new[] { "Draw" }), ct);

        _repositoryMock.Verify(r => r.GetByIdAsync(sessionId, ct), Times.Once);
        _repositoryMock.Verify(r => r.UpdateAsync(session, ct), Times.Once);
    }
}

#endregion

#region TriggerEventSnapshotCommandHandler

/// <summary>
/// Tests for TriggerEventSnapshotCommandHandler with debounce logic.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class TriggerEventSnapshotCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly FakeTimeProvider _timeProvider;

    public TriggerEventSnapshotCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionRepository>();
        _mediatorMock = new Mock<IMediator>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 19, 14, 0, 0, TimeSpan.Zero));
    }

    private TriggerEventSnapshotCommandHandler CreateHandler() =>
        new(_repositoryMock.Object, _mediatorMock.Object, _timeProvider);

    private LiveGameSession CreateStartedSessionWithConfig(
        Guid? sessionId = null, SnapshotTriggerConfig? config = null)
    {
        var session = LiveGameSession.Create(
            sessionId ?? Guid.NewGuid(), Guid.NewGuid(), "Test Game", _timeProvider);
        session.AddPlayer(Guid.NewGuid(), "Player 1", PlayerColor.Red, _timeProvider);
        session.Start(_timeProvider);
        if (config != null)
            session.SetSnapshotTriggerConfig(config, _timeProvider);
        return session;
    }

    private static SessionSnapshotDto CreateMockSnapshotDto(Guid sessionId) =>
        new(Guid.NewGuid(), sessionId, 0, SnapshotTrigger.PhaseAdvanced,
            "Test snapshot", true, 0, 0, DateTime.UtcNow, null);

    [Fact]
    public async Task Handle_NoConfig_CreatesSnapshot()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSessionWithConfig(sessionId);
        var expectedDto = CreateMockSnapshotDto(sessionId);

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new TriggerEventSnapshotCommand(sessionId, SnapshotTrigger.PhaseAdvanced, "Test", null),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(expectedDto.Id, result!.Id);
        _mediatorMock.Verify(m => m.Send(
            It.Is<CreateSnapshotCommand>(c =>
                c.SessionId == sessionId &&
                c.TriggerType == SnapshotTrigger.PhaseAdvanced),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TriggerDisabled_ReturnsNull()
    {
        var sessionId = Guid.NewGuid();
        // Config with only TurnAdvanced enabled (not PhaseAdvanced)
        var config = new SnapshotTriggerConfig(new[] { SnapshotTrigger.TurnAdvanced });
        var session = CreateStartedSessionWithConfig(sessionId, config);

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new TriggerEventSnapshotCommand(sessionId, SnapshotTrigger.PhaseAdvanced, "Test", null),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithinDebouncePeriod_ReturnsNull()
    {
        var sessionId = Guid.NewGuid();
        var config = SnapshotTriggerConfig.CreateDefault(); // 5s debounce
        var session = CreateStartedSessionWithConfig(sessionId, config);

        // Set last snapshot to 2 seconds ago
        var twoSecondsAgo = _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(-2);
        session.RecordSnapshotTimestamp(twoSecondsAgo);

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new TriggerEventSnapshotCommand(sessionId, SnapshotTrigger.PhaseAdvanced, "Test", null),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AfterDebouncePeriod_CreatesSnapshot()
    {
        var sessionId = Guid.NewGuid();
        var config = SnapshotTriggerConfig.CreateDefault(); // 5s debounce
        var session = CreateStartedSessionWithConfig(sessionId, config);

        // Set last snapshot to 10 seconds ago
        var tenSecondsAgo = _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(-10);
        session.RecordSnapshotTimestamp(tenSecondsAgo);

        var expectedDto = CreateMockSnapshotDto(sessionId);

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new TriggerEventSnapshotCommand(sessionId, SnapshotTrigger.PhaseAdvanced, "Test", null),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulSnapshot_RecordsTimestampAndUpdatesSession()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSessionWithConfig(sessionId);
        var expectedDto = CreateMockSnapshotDto(sessionId);

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var handler = CreateHandler();
        await handler.Handle(
            new TriggerEventSnapshotCommand(sessionId, SnapshotTrigger.PhaseAdvanced, "Test", null),
            TestContext.Current.CancellationToken);

        Assert.NotNull(session.LastSnapshotTimestamp);
        _repositoryMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new TriggerEventSnapshotCommand(sessionId, SnapshotTrigger.PhaseAdvanced, "Test", null),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNull()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_PassesDescriptionToCreateCommand()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSessionWithConfig(sessionId);
        var expectedDto = CreateMockSnapshotDto(sessionId);
        var description = "Phase advanced to Action";
        var playerId = Guid.NewGuid();

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var handler = CreateHandler();
        await handler.Handle(
            new TriggerEventSnapshotCommand(sessionId, SnapshotTrigger.ManualSave, description, playerId),
            TestContext.Current.CancellationToken);

        _mediatorMock.Verify(m => m.Send(
            It.Is<CreateSnapshotCommand>(c =>
                c.SessionId == sessionId &&
                c.TriggerType == SnapshotTrigger.ManualSave &&
                c.TriggerDescription == description &&
                c.CreatedByPlayerId == playerId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

#endregion
