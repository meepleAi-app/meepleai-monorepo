using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using FluentAssertions;
using DomainSessionSnapshot = Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;

/// <summary>
/// Tests for RestoreSessionSnapshotCommandHandler.
/// Issue #5581: Auto-snapshot on Pause + snapshot history.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class RestoreSessionSnapshotCommandHandlerTests
{
    private readonly Mock<ISessionSnapshotRepository> _snapshotRepositoryMock;
    private readonly Mock<ILiveSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly FakeTimeProvider _timeProvider;

    public RestoreSessionSnapshotCommandHandlerTests()
    {
        _snapshotRepositoryMock = new Mock<ISessionSnapshotRepository>();
        _sessionRepositoryMock = new Mock<ILiveSessionRepository>();
        _mediatorMock = new Mock<IMediator>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 9, 10, 0, 0, TimeSpan.Zero));
    }

    private RestoreSessionSnapshotCommandHandler CreateHandler() =>
        new(_snapshotRepositoryMock.Object,
            _sessionRepositoryMock.Object,
            _mediatorMock.Object,
            _timeProvider);

    private LiveGameSession CreateActiveSession(Guid? sessionId = null)
    {
        var id = sessionId ?? Guid.NewGuid();
        var session = LiveGameSession.Create(id, Guid.NewGuid(), "Test Game", _timeProvider);
        session.AddPlayer(Guid.NewGuid(), "Player 1", PlayerColor.Red, _timeProvider);
        session.Start(_timeProvider);

        // Set some game state
        var gameState = JsonDocument.Parse("{\"board\":\"initial\",\"score\":0}");
        session.UpdateGameState(gameState, _timeProvider);

        return session;
    }

    private static DomainSessionSnapshot CreateCheckpointSnapshot(
        Guid sessionId, int index = 0, string stateJson = "{\"board\":\"saved\",\"score\":42}")
    {
        return new DomainSessionSnapshot(
            Guid.NewGuid(), sessionId, index,
            SnapshotTrigger.ManualSave, "Test checkpoint",
            stateJson, isCheckpoint: true, turnIndex: 1, phaseIndex: 0,
            createdByPlayerId: null);
    }

    private static SessionSnapshotDto CreatePreRestoreDto(Guid sessionId) =>
        new(Guid.NewGuid(), sessionId, 5, SnapshotTrigger.PreRestore,
            "Auto \u2014 Pre-restore turno 1", true, 1, 0, DateTime.UtcNow, null);

    [Fact]
    public async Task Handle_ValidRestore_CreatesPreRestoreSnapshotAndRestoresState()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId);
        var checkpoint = CreateCheckpointSnapshot(sessionId);
        var preRestoreDto = CreatePreRestoreDto(sessionId);

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _snapshotRepositoryMock.Setup(r => r.GetBySessionAndIndexAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkpoint);
        _snapshotRepositoryMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DomainSessionSnapshot> { checkpoint });
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preRestoreDto);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new RestoreSessionSnapshotCommand(sessionId, 0), TestContext.Current.CancellationToken);

        // Verify pre-restore snapshot was created
        _mediatorMock.Verify(m => m.Send(
            It.Is<CreateSnapshotCommand>(c =>
                c.SessionId == sessionId &&
                c.TriggerType == SnapshotTrigger.PreRestore &&
                c.TriggerDescription!.StartsWith("Auto \u2014 Pre-restore", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify session was updated
        _sessionRepositoryMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);

        result.Should().NotBeNull();
        result.Id.Should().Be(preRestoreDto.Id);
    }

    [Fact]
    public async Task Handle_RestoresGameStateFromSnapshot()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId);
        var savedState = "{\"board\":\"restored\",\"score\":99}";
        var checkpoint = CreateCheckpointSnapshot(sessionId, stateJson: savedState);
        var preRestoreDto = CreatePreRestoreDto(sessionId);

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _snapshotRepositoryMock.Setup(r => r.GetBySessionAndIndexAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkpoint);
        _snapshotRepositoryMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DomainSessionSnapshot> { checkpoint });
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preRestoreDto);

        var handler = CreateHandler();
        await handler.Handle(
            new RestoreSessionSnapshotCommand(sessionId, 0), TestContext.Current.CancellationToken);

        // Verify the game state was restored from the snapshot
        session.GameState.Should().NotBeNull();
        var restoredJson = session.GameState.RootElement.GetRawText();
        restoredJson.Should().Contain("\"restored\"");
        restoredJson.Should().Contain("99");
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = CreateHandler();

        var act = () =>
            handler.Handle(
                new RestoreSessionSnapshotCommand(sessionId, 0),
                TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CompletedSession_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId);
        session.Complete(_timeProvider); // Complete the session

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();

        var act = () =>
            handler.Handle(
                new RestoreSessionSnapshotCommand(sessionId, 0),
                TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_SnapshotNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId);

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _snapshotRepositoryMock.Setup(r => r.GetBySessionAndIndexAsync(sessionId, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainSessionSnapshot?)null);

        var handler = CreateHandler();

        var act = () =>
            handler.Handle(
                new RestoreSessionSnapshotCommand(sessionId, 99),
                TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NoCheckpointFound_ThrowsConflictException()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId);
        var checkpoint = CreateCheckpointSnapshot(sessionId);

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _snapshotRepositoryMock.Setup(r => r.GetBySessionAndIndexAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkpoint);
        _snapshotRepositoryMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DomainSessionSnapshot>()); // Empty

        var handler = CreateHandler();

        var act = () =>
            handler.Handle(
                new RestoreSessionSnapshotCommand(sessionId, 0),
                TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNull()
    {
        var handler = CreateHandler();

        var act = () =>
            handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId);
        var checkpoint = CreateCheckpointSnapshot(sessionId);
        var preRestoreDto = CreatePreRestoreDto(sessionId);
        var ct = TestContext.Current.CancellationToken;

        _sessionRepositoryMock.Setup(r => r.GetByIdAsync(sessionId, ct))
            .ReturnsAsync(session);
        _snapshotRepositoryMock.Setup(r => r.GetBySessionAndIndexAsync(sessionId, 0, ct))
            .ReturnsAsync(checkpoint);
        _snapshotRepositoryMock.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 0, ct))
            .ReturnsAsync(new List<DomainSessionSnapshot> { checkpoint });
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateSnapshotCommand>(), ct))
            .ReturnsAsync(preRestoreDto);

        var handler = CreateHandler();
        await handler.Handle(new RestoreSessionSnapshotCommand(sessionId, 0), ct);

        _sessionRepositoryMock.Verify(r => r.GetByIdAsync(sessionId, ct), Times.Once);
        _snapshotRepositoryMock.Verify(r => r.GetBySessionAndIndexAsync(sessionId, 0, ct), Times.Once);
    }
}
