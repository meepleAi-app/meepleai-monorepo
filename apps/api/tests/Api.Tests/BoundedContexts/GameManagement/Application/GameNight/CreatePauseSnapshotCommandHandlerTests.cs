using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentValidation.TestHelper;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for <see cref="CreatePauseSnapshotCommandHandler"/>.
/// Game Night Improvvisata — E4: Save/Resume flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreatePauseSnapshotCommandHandlerTests
{
    // ─── Fixtures ─────────────────────────────────────────────────────────────

    private static readonly Guid TestSessionId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();

    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<IPauseSnapshotRepository> _snapshotRepoMock;
    private readonly Mock<ITierEnforcementService> _tierEnforcementMock;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly CreatePauseSnapshotCommandHandler _sut;

    public CreatePauseSnapshotCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _snapshotRepoMock = new Mock<IPauseSnapshotRepository>();
        _tierEnforcementMock = new Mock<ITierEnforcementService>();
        _publisherMock = new Mock<IPublisher>();

        _snapshotRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _publisherMock
            .Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default: session save is enabled (Premium plan)
        _tierEnforcementMock
            .Setup(t => t.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.PremiumTier);

        _sut = new CreatePauseSnapshotCommandHandler(
            _sessionRepoMock.Object,
            _snapshotRepoMock.Object,
            _dbContext,
            _tierEnforcementMock.Object,
            _publisherMock.Object,
            NullLogger<CreatePauseSnapshotCommandHandler>.Instance);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private LiveGameSession CreateInProgressSession(
        Guid? id = null,
        bool withDbEntity = false,
        string gameName = "Wingspan")
    {
        var sessionId = id ?? TestSessionId;
        var session = LiveGameSession.Create(
            id: sessionId,
            createdByUserId: TestUserId,
            gameName: gameName,
            timeProvider: TimeProvider.System);

        session.AddPlayer(
            userId: TestUserId,
            displayName: "Alice",
            color: PlayerColor.Red,
            timeProvider: TimeProvider.System,
            role: PlayerRole.Host);

        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _sessionRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        if (withDbEntity)
        {
            _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
            {
                Id = sessionId,
                SessionCode = session.SessionCode,
                GameName = gameName,
                CreatedByUserId = TestUserId,
                Status = (int)LiveSessionStatus.InProgress,
                CurrentTurnIndex = session.CurrentTurnIndex,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                AgentMode = (int)AgentSessionMode.None,
                ScoringConfigJson = "{}",
                RowVersion = new byte[] { 1 }
            });
            _dbContext.SaveChanges();
        }

        return session;
    }

    private static CreatePauseSnapshotCommand BuildCommand(
        Guid? sessionId = null,
        Guid? userId = null,
        List<Guid>? photoIds = null)
        => new(
            SessionId: sessionId ?? TestSessionId,
            SavedByUserId: userId ?? TestUserId,
            FinalPhotoIds: photoIds);

    // ─── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HappyPath_ReturnsSnapshotId()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act
        var snapshotId = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, snapshotId);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesSnapshotWithCorrectState()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        PauseSnapshot? capturedSnapshot = null;
        _snapshotRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<PauseSnapshot, CancellationToken>(
                (s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedSnapshot);
        Assert.Equal(TestSessionId, capturedSnapshot!.LiveGameSessionId);
        Assert.Equal(TestUserId, capturedSnapshot.SavedByUserId);
        Assert.False(capturedSnapshot.IsAutoSave);
        Assert.True(capturedSnapshot.CurrentTurn >= 1, "CurrentTurn should be at least 1 after Start()");
    }

    [Fact]
    public async Task Handle_HappyPath_PausesSession()
    {
        // Arrange
        var session = CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        LiveGameSession? capturedSession = null;
        _sessionRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => capturedSession = s)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedSession);
        Assert.Equal(LiveSessionStatus.Paused, capturedSession!.Status);
    }

    [Fact]
    public async Task Handle_HappyPath_IncludesPlayerScoresInSnapshot()
    {
        // Arrange
        var session = CreateInProgressSession(withDbEntity: true);

        // Session has 1 player (Alice) added in helper
        var command = BuildCommand();

        PauseSnapshot? capturedSnapshot = null;
        _snapshotRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<PauseSnapshot, CancellationToken>(
                (s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedSnapshot);
        Assert.Single(capturedSnapshot!.PlayerScores);
        Assert.Equal("Alice", capturedSnapshot.PlayerScores[0].PlayerName);
    }

    [Fact]
    public async Task Handle_HappyPath_IncludesFinalPhotoIdsInSnapshot()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);
        var photoIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var command = BuildCommand(photoIds: photoIds);

        PauseSnapshot? capturedSnapshot = null;
        _snapshotRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<PauseSnapshot, CancellationToken>(
                (s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedSnapshot);
        Assert.Equal(2, capturedSnapshot!.AttachmentIds.Count);
        Assert.All(photoIds, id => Assert.Contains(id, capturedSnapshot.AttachmentIds));
    }

    [Fact]
    public async Task Handle_HappyPath_PublishesSessionPausedEvent()
    {
        // Arrange
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _publisherMock.Verify(
            p => p.Publish(
                It.Is<SessionPausedEvent>(e => e.SessionId == TestSessionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_HappyPath_WithAgentMode_PublishesSessionSaveRequestedEvent()
    {
        // Arrange — session with an active agent
        var chatSessionId = Guid.NewGuid();
        var session = LiveGameSession.Create(
            id: TestSessionId,
            createdByUserId: TestUserId,
            gameName: "Wingspan",
            timeProvider: TimeProvider.System);

        session.AddPlayer(TestUserId, "Alice", PlayerColor.Red, TimeProvider.System, PlayerRole.Host);
        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System);
        session.SetAgentMode(AgentSessionMode.Assistant, chatSessionId, TimeProvider.System);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _sessionRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dbContext.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = TestSessionId,
            SessionCode = session.SessionCode,
            GameName = "Wingspan",
            CreatedByUserId = TestUserId,
            Status = (int)LiveSessionStatus.InProgress,
            CurrentTurnIndex = session.CurrentTurnIndex,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            AgentMode = (int)AgentSessionMode.Assistant,
            ScoringConfigJson = "{}",
            RowVersion = new byte[] { 1 }
        });
        await _dbContext.SaveChangesAsync();

        var command = BuildCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _publisherMock.Verify(
            p => p.Publish(
                It.Is<SessionSaveRequestedEvent>(e =>
                    e.LiveGameSessionId == TestSessionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Error cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ThrowsNotFoundException()
    {
        // Arrange — repository returns null
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var command = BuildCommand();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSessionIsNotInProgress_ThrowsConflictException()
    {
        // Arrange — session in Created status (not started)
        var session = LiveGameSession.Create(
            id: TestSessionId,
            createdByUserId: TestUserId,
            gameName: "Test Game",
            timeProvider: TimeProvider.System);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = BuildCommand();

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenSessionIsAlreadyPaused_ThrowsConflictException()
    {
        // Arrange
        var session = LiveGameSession.Create(
            id: TestSessionId,
            createdByUserId: TestUserId,
            gameName: "Test Game",
            timeProvider: TimeProvider.System);

        session.AddPlayer(TestUserId, "Alice", PlayerColor.Red, TimeProvider.System, PlayerRole.Host);
        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System);
        session.Pause(TimeProvider.System); // Already paused

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(TestSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = BuildCommand();

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithoutAgent_DoesNotPublishSessionSaveRequestedEvent()
    {
        // Arrange — no agent mode set (default = None)
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert — SessionSaveRequestedEvent should NOT be published when no agent
        _publisherMock.Verify(
            p => p.Publish(
                It.IsAny<SessionSaveRequestedEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Tier enforcement ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionSaveNotEnabledForTier_ThrowsConflictException()
    {
        // Arrange — Free tier does not include session save
        _tierEnforcementMock
            .Setup(t => t.GetLimitsAsync(TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.FreeTier);

        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));

        Assert.Contains("salvataggio della sessione", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Premium", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Snapshot repository must not be called when tier blocks the save
        _snapshotRepoMock.Verify(
            r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionSaveEnabledForTier_Succeeds()
    {
        // Arrange — default mock uses PremiumTier (SessionSaveEnabled = true)
        CreateInProgressSession(withDbEntity: true);
        var command = BuildCommand();

        // Act
        var snapshotId = await _sut.Handle(command, CancellationToken.None);

        // Assert — snapshot was created successfully
        Assert.NotEqual(Guid.Empty, snapshotId);
        _snapshotRepoMock.Verify(
            r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

/// <summary>
/// Validation tests for <see cref="CreatePauseSnapshotCommandValidator"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreatePauseSnapshotCommandValidatorTests
{
    private readonly CreatePauseSnapshotCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        var command = new CreatePauseSnapshotCommand(Guid.NewGuid(), Guid.NewGuid());
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySessionId_Fails()
    {
        var command = new CreatePauseSnapshotCommand(Guid.Empty, Guid.NewGuid());
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validate_WithEmptyUserId_Fails()
    {
        var command = new CreatePauseSnapshotCommand(Guid.NewGuid(), Guid.Empty);
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.SavedByUserId);
    }
}
