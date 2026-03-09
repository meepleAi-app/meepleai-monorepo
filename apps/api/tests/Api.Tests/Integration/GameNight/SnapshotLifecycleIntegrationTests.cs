using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameNight;

/// <summary>
/// Integration tests for the full snapshot lifecycle:
/// create session → create snapshot → verify checkpoint → restore → verify state.
/// Uses Moq mocks to exercise the command handlers end-to-end.
/// Issue #5589: Game Night snapshot lifecycle integration tests.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameNight")]
[Trait("Issue", "5589")]
public sealed class SnapshotLifecycleIntegrationTests
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private static LiveGameSession CreateActiveSession(Guid sessionId, JsonDocument? gameState = null)
    {
        var session = LiveGameSession.Create(
            sessionId,
            createdByUserId: Guid.NewGuid(),
            gameName: "Catan",
            gameId: Guid.NewGuid(),
            agentMode: AgentSessionMode.Assistant);

        session.AddPlayer(null, "Host", PlayerColor.Red);
        session.Start();

        if (gameState != null)
        {
            session.UpdateGameState(gameState, TimeProvider.System);
        }

        return session;
    }

    // =========================================================================
    // 1. Create snapshot — first snapshot is always checkpoint
    // =========================================================================

    [Fact]
    public async Task CreateSnapshot_FirstSnapshot_IsCheckpointAtIndex0()
    {
        var sessionId = Guid.NewGuid();
        var state = JsonDocument.Parse("{\"turn\":1,\"scores\":{\"Alice\":5}}");
        var session = CreateActiveSession(sessionId, state);

        var snapshotRepo = new Mock<ISessionSnapshotRepository>();
        snapshotRepo.Setup(r => r.GetLatestBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionSnapshot?)null);

        var sessionRepo = new Mock<ILiveSessionRepository>();
        sessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var attachmentRepo = new Mock<ISessionAttachmentRepository>();
        attachmentRepo.Setup(r => r.GetBySnapshotAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionAttachment>());

        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new CreateSnapshotCommandHandler(
            snapshotRepo.Object, sessionRepo.Object, attachmentRepo.Object, unitOfWork.Object);

        var command = new CreateSnapshotCommand(
            sessionId, SnapshotTrigger.ManualSave, "Initial save", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.SnapshotIndex.Should().Be(0);
        result.IsCheckpoint.Should().BeTrue();
        result.TriggerType.Should().Be(SnapshotTrigger.ManualSave);
        result.TriggerDescription.Should().Be("Initial save");

        snapshotRepo.Verify(r => r.AddAsync(
            It.Is<SessionSnapshot>(s =>
                s.SnapshotIndex == 0 &&
                s.IsCheckpoint &&
                s.TriggerType == SnapshotTrigger.ManualSave),
            It.IsAny<CancellationToken>()), Times.Once);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    // 2. Create snapshot — subsequent snapshot stores delta
    // =========================================================================

    [Fact]
    public async Task CreateSnapshot_SecondSnapshot_StoresDelta()
    {
        var sessionId = Guid.NewGuid();
        var state = JsonDocument.Parse("{\"turn\":2,\"scores\":{\"Alice\":10}}");
        var session = CreateActiveSession(sessionId, state);

        var firstSnapshot = new SessionSnapshot(
            Guid.NewGuid(), sessionId, snapshotIndex: 0,
            SnapshotTrigger.ManualSave, "Initial",
            deltaDataJson: "{\"turn\":1,\"scores\":{\"Alice\":5}}",
            isCheckpoint: true, turnIndex: 0, phaseIndex: null,
            createdByPlayerId: null);

        var snapshotRepo = new Mock<ISessionSnapshotRepository>();
        snapshotRepo.Setup(r => r.GetLatestBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstSnapshot);
        snapshotRepo.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionSnapshot> { firstSnapshot });

        var sessionRepo = new Mock<ILiveSessionRepository>();
        sessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var attachmentRepo = new Mock<ISessionAttachmentRepository>();
        attachmentRepo.Setup(r => r.GetBySnapshotAsync(sessionId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionAttachment>());

        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new CreateSnapshotCommandHandler(
            snapshotRepo.Object, sessionRepo.Object, attachmentRepo.Object, unitOfWork.Object);

        var command = new CreateSnapshotCommand(
            sessionId, SnapshotTrigger.ManualSave, "Turn 2 save", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.SnapshotIndex.Should().Be(1);
        result.IsCheckpoint.Should().BeFalse();
        result.TriggerType.Should().Be(SnapshotTrigger.ManualSave);
    }

    // =========================================================================
    // 3. Checkpoint interval — every 10th snapshot is checkpoint
    // =========================================================================

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(5, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(20, true)]
    [InlineData(11, false)]
    public void ShouldBeCheckpoint_FollowsIntervalRules(int index, bool expected)
    {
        SessionSnapshot.ShouldBeCheckpoint(index).Should().Be(expected);
    }

    // =========================================================================
    // 4. Nearest checkpoint index
    // =========================================================================

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 10)]
    [InlineData(15, 10)]
    [InlineData(25, 20)]
    public void GetNearestCheckpointIndex_ReturnsCorrectIndex(int snapshotIndex, int expectedCheckpoint)
    {
        SessionSnapshot.GetNearestCheckpointIndex(snapshotIndex).Should().Be(expectedCheckpoint);
    }

    // =========================================================================
    // 5. Restore snapshot — creates pre-restore snapshot and overwrites state
    // =========================================================================

    [Fact]
    public async Task RestoreSnapshot_ActiveSession_CreatesPreRestoreAndUpdatesState()
    {
        var sessionId = Guid.NewGuid();
        var originalState = "{\"turn\":5,\"scores\":{\"Alice\":25}}";
        var session = CreateActiveSession(sessionId,
            JsonDocument.Parse("{\"turn\":10,\"scores\":{\"Alice\":50}}"));

        var targetSnapshot = new SessionSnapshot(
            Guid.NewGuid(), sessionId, snapshotIndex: 0,
            SnapshotTrigger.ManualSave, "Initial",
            deltaDataJson: originalState,
            isCheckpoint: true, turnIndex: 0, phaseIndex: null,
            createdByPlayerId: null);

        var snapshotRepo = new Mock<ISessionSnapshotRepository>();
        snapshotRepo.Setup(r => r.GetBySessionAndIndexAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetSnapshot);
        snapshotRepo.Setup(r => r.GetSnapshotsForReconstructionAsync(sessionId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionSnapshot> { targetSnapshot });

        var sessionRepo = new Mock<ILiveSessionRepository>();
        sessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot.SessionSnapshotDto(
                Id: Guid.NewGuid(), SessionId: sessionId, SnapshotIndex: 11,
                TriggerType: SnapshotTrigger.PreRestore,
                TriggerDescription: "Auto \u2014 Pre-restore turno 0",
                IsCheckpoint: false, TurnIndex: 0, PhaseIndex: null,
                Timestamp: DateTime.UtcNow, CreatedByPlayerId: null));

        var handler = new RestoreSessionSnapshotCommandHandler(
            snapshotRepo.Object, sessionRepo.Object, mediator.Object, TimeProvider.System);

        var command = new RestoreSessionSnapshotCommand(sessionId, 0);

        var result = await handler.Handle(command, CancellationToken.None);

        // Pre-restore snapshot was created
        mediator.Verify(m => m.Send(
            It.Is<CreateSnapshotCommand>(c =>
                c.SessionId == sessionId &&
                c.TriggerType == SnapshotTrigger.PreRestore),
            It.IsAny<CancellationToken>()), Times.Once);

        // Session state was updated
        sessionRepo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);

        result.TriggerType.Should().Be(SnapshotTrigger.PreRestore);
    }

    // =========================================================================
    // 6. Restore snapshot — fails on completed session
    // =========================================================================

    [Fact]
    public async Task RestoreSnapshot_CompletedSession_ThrowsConflict()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId);
        session.Complete();

        var sessionRepo = new Mock<ILiveSessionRepository>();
        sessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new RestoreSessionSnapshotCommandHandler(
            new Mock<ISessionSnapshotRepository>().Object,
            sessionRepo.Object,
            new Mock<IMediator>().Object,
            TimeProvider.System);

        var command = new RestoreSessionSnapshotCommand(sessionId, 0);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*active*");
    }

    // =========================================================================
    // 7. Restore snapshot — fails if snapshot not found
    // =========================================================================

    [Fact]
    public async Task RestoreSnapshot_SnapshotNotFound_ThrowsNotFound()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateActiveSession(sessionId);

        var sessionRepo = new Mock<ILiveSessionRepository>();
        sessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var snapshotRepo = new Mock<ISessionSnapshotRepository>();
        snapshotRepo.Setup(r => r.GetBySessionAndIndexAsync(sessionId, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionSnapshot?)null);

        var handler = new RestoreSessionSnapshotCommandHandler(
            snapshotRepo.Object, sessionRepo.Object,
            new Mock<IMediator>().Object, TimeProvider.System);

        var command = new RestoreSessionSnapshotCommand(sessionId, 99);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // =========================================================================
    // 8. JsonDeltaHelper — delta computation and application
    // =========================================================================

    [Fact]
    public void JsonDeltaHelper_ComputeAndApply_RoundTrips()
    {
        var before = "{\"turn\":1,\"scores\":{\"Alice\":5}}";
        var after = "{\"turn\":2,\"scores\":{\"Alice\":10,\"Bob\":3}}";

        var delta = JsonDeltaHelper.ComputeDelta(before, after);
        var reconstructed = JsonDeltaHelper.ApplyDelta(before, delta);

        using var actualDoc = JsonDocument.Parse(reconstructed);

        actualDoc.RootElement.GetProperty("turn").GetInt32().Should().Be(2);
        actualDoc.RootElement.GetProperty("scores").GetProperty("Alice").GetInt32().Should().Be(10);
        actualDoc.RootElement.GetProperty("scores").GetProperty("Bob").GetInt32().Should().Be(3);
    }

    [Fact]
    public void JsonDeltaHelper_ApplyDelta_EmptyDelta_ReturnsSameState()
    {
        var state = "{\"turn\":1}";
        var delta = "[]";

        var result = JsonDeltaHelper.ApplyDelta(state, delta);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("turn").GetInt32().Should().Be(1);
    }

    // =========================================================================
    // 9. SessionSnapshot domain validation
    // =========================================================================

    [Fact]
    public void SessionSnapshot_EmptySessionId_Throws()
    {
        var act = () => new SessionSnapshot(
            Guid.NewGuid(), sessionId: Guid.Empty, snapshotIndex: 0,
            SnapshotTrigger.ManualSave, "test", "{}", isCheckpoint: true,
            turnIndex: 0, phaseIndex: null, createdByPlayerId: null);

        act.Should().Throw<ArgumentException>().WithMessage("*SessionId*");
    }

    [Fact]
    public void SessionSnapshot_NegativeIndex_Throws()
    {
        var act = () => new SessionSnapshot(
            Guid.NewGuid(), sessionId: Guid.NewGuid(), snapshotIndex: -1,
            SnapshotTrigger.ManualSave, "test", "{}", isCheckpoint: true,
            turnIndex: 0, phaseIndex: null, createdByPlayerId: null);

        act.Should().Throw<ArgumentException>().WithMessage("*SnapshotIndex*");
    }

    [Fact]
    public void SessionSnapshot_NegativeTurnIndex_Throws()
    {
        var act = () => new SessionSnapshot(
            Guid.NewGuid(), sessionId: Guid.NewGuid(), snapshotIndex: 0,
            SnapshotTrigger.ManualSave, "test", "{}", isCheckpoint: true,
            turnIndex: -1, phaseIndex: null, createdByPlayerId: null);

        act.Should().Throw<ArgumentException>().WithMessage("*TurnIndex*");
    }

    [Fact]
    public void SessionSnapshot_EmptyDeltaJson_DefaultsToEmptyObject()
    {
        var snapshot = new SessionSnapshot(
            Guid.NewGuid(), sessionId: Guid.NewGuid(), snapshotIndex: 0,
            SnapshotTrigger.ManualSave, "test", deltaDataJson: "",
            isCheckpoint: true, turnIndex: 0, phaseIndex: null,
            createdByPlayerId: null);

        snapshot.DeltaDataJson.Should().Be("{}");
    }
}
