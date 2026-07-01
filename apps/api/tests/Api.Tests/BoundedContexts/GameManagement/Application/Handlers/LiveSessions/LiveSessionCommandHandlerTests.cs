using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Comprehensive unit tests for all live session command handlers.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveSessionCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repositoryMock;
    private readonly Mock<IGameSessionRepository> _gameSessionRepositoryMock;
    private readonly Mock<ISessionQuotaService> _quotaServiceMock;
    private readonly TimeProvider _timeProvider;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    // #2501 SP0: companion-saga ACL dependency. Default mock returns Guid.Empty for
    // CreateCompanionAsync, which is harmless for these existing assertions (they verify
    // AddAsync/captured properties, not TrackingSessionId). Companion behavior itself is
    // covered by CreateLiveSessionCommandHandlerTests (dedicated saga unit tests).
    private readonly Mock<ICompanionSessionService> _companionSessionServiceMock;

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly UserTier DefaultUserTier = UserTier.Premium;
    private static readonly Role DefaultUserRole = Role.Admin;

    public LiveSessionCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionRepository>();
        _gameSessionRepositoryMock = new Mock<IGameSessionRepository>();
        _quotaServiceMock = new Mock<ISessionQuotaService>();
        _timeProvider = TimeProvider.System;
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _companionSessionServiceMock = new Mock<ICompanionSessionService>();

        // Default: quota allowed (Premium/Admin bypass)
        _quotaServiceMock
            .Setup(q => q.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UserTier>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Unlimited());
    }

    // === Shared Helpers ===

    /// <summary>
    /// Creates a LiveGameSession in Created status (default from factory).
    /// </summary>
    private static LiveGameSession CreateSessionInCreatedStatus(Guid? sessionId = null, Guid? userId = null)
    {
        return LiveGameSession.Create(
            sessionId ?? Guid.NewGuid(),
            userId ?? DefaultUserId,
            "Test Game",
            TimeProvider.System);
    }

    /// <summary>
    /// Creates a session with a player added (needed for Start and other lifecycle transitions).
    /// </summary>
    private static LiveGameSession CreateSessionWithPlayer(Guid? sessionId = null, Guid? userId = null)
    {
        var session = CreateSessionInCreatedStatus(sessionId, userId);
        session.AddPlayer(null, "Player One", PlayerColor.Red, TimeProvider.System);
        return session;
    }

    /// <summary>
    /// Creates a session in InProgress status (Created + AddPlayer + Start).
    /// </summary>
    private static LiveGameSession CreateSessionInProgress(Guid? sessionId = null, Guid? userId = null)
    {
        var session = CreateSessionWithPlayer(sessionId, userId);
        session.Start(TimeProvider.System);
        return session;
    }

    /// <summary>
    /// Creates a session in Paused status (Created + AddPlayer + Start + Pause).
    /// </summary>
    private static LiveGameSession CreateSessionPaused(Guid? sessionId = null, Guid? userId = null)
    {
        var session = CreateSessionInProgress(sessionId, userId);
        session.Pause(TimeProvider.System);
        return session;
    }

    /// <summary>
    /// Sets up the repository mock to return the given session for GetByIdAsync.
    /// </summary>
    private void SetupRepoGetById(Guid sessionId, LiveGameSession? session)
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    // =============================================================================
    // region CreateLiveSessionCommandHandler
    // =============================================================================

    #region CreateLiveSessionCommandHandler

    [Fact]
    public async Task CreateLiveSession_HappyPath_DefaultConfig_ReturnsSessionId()
    {
        // Arrange
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object, _companionSessionServiceMock.Object);
        var command = new CreateLiveSessionCommand(DefaultUserId, "Catan");

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBe(Guid.Empty);
        _repositoryMock.Verify(
            r => r.AddAsync(It.Is<LiveGameSession>(s =>
                s.GameName == "Catan" &&
                s.CreatedByUserId == DefaultUserId &&
                s.Status == LiveSessionStatus.Created),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateLiveSession_HappyPath_CustomScoring_ReturnsSessionId()
    {
        // Arrange
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object, _companionSessionServiceMock.Object);
        var scoringDimensions = new List<string> { "Points", "Bonus" };
        var dimensionUnits = new Dictionary<string, string> { { "Points", "pts" }, { "Bonus", "pts" } };
        var command = new CreateLiveSessionCommand(
            DefaultUserId,
            "Terraforming Mars",
            GameId: Guid.NewGuid(),
            ScoringDimensions: scoringDimensions,
            DimensionUnits: dimensionUnits);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBe(Guid.Empty);
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object, _companionSessionServiceMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateLiveSession_VerifiesAddAsyncCalled()
    {
        // Arrange
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object, _companionSessionServiceMock.Object);
        var command = new CreateLiveSessionCommand(DefaultUserId, "Ticket to Ride");

        LiveGameSession? capturedSession = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => capturedSession = s)
            .Returns(Task.CompletedTask);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        capturedSession.Should().NotBeNull();
        capturedSession.Id.Should().Be(result);
        capturedSession.GameName.Should().Be("Ticket to Ride");
        capturedSession.CreatedByUserId.Should().Be(DefaultUserId);
    }

    [Fact]
    public async Task CreateLiveSession_WithAllOptionalParams_CreatesCorrectly()
    {
        // Arrange
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object, _companionSessionServiceMock.Object);
        var gameId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        // #2552: with a non-null GameId the companion saga runs; configure the mock to return a real
        // id so we can assert it flows into TrackingSessionId (the default mock returned Guid.Empty
        // silently and the assertion below was missing).
        var expectedTrackingSessionId = Guid.NewGuid();
        _companionSessionServiceMock
            .Setup(s => s.CreateCompanionAsync(DefaultUserId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTrackingSessionId);

        var command = new CreateLiveSessionCommand(
            DefaultUserId,
            "Spirit Island",
            GameId: gameId,
            Visibility: PlayRecordVisibility.Group,
            GroupId: groupId,
            AgentMode: AgentSessionMode.Assistant);

        LiveGameSession? capturedSession = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => capturedSession = s)
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        capturedSession.Should().NotBeNull();
        capturedSession.GameId.Should().Be(gameId);
        capturedSession.Visibility.Should().Be(PlayRecordVisibility.Group);
        capturedSession.GroupId.Should().Be(groupId);
        capturedSession.AgentMode.Should().Be(AgentSessionMode.Assistant);
        capturedSession.TrackingSessionId.Should().Be(expectedTrackingSessionId);
        _companionSessionServiceMock.Verify(
            s => s.CreateCompanionAsync(DefaultUserId, gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    // =============================================================================
    // region StartLiveSessionCommandHandler
    // =============================================================================

    #region StartLiveSessionCommandHandler

    [Fact]
    public async Task StartLiveSession_HappyPath_StartsSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithPlayer(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = new StartLiveSessionCommandHandler(_repositoryMock.Object, _gameSessionRepositoryMock.Object, _quotaServiceMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new StartLiveSessionCommand(sessionId, DefaultUserId, DefaultUserTier, DefaultUserRole);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.Status.Should().Be(LiveSessionStatus.InProgress);
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartLiveSession_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new StartLiveSessionCommandHandler(_repositoryMock.Object, _gameSessionRepositoryMock.Object, _quotaServiceMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new StartLiveSessionCommand(sessionId, DefaultUserId, DefaultUserTier, DefaultUserRole);

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task StartLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new StartLiveSessionCommandHandler(_repositoryMock.Object, _gameSessionRepositoryMock.Object, _quotaServiceMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ─── Issue #2608: Creator-only guard ──────────────────────────────────────────

    [Fact(DisplayName = "#2608: Non-creator caller → ForbiddenException, no quota check, no GameSession created, no start")]
    public async Task StartLiveSession_NonCreatorCaller_ThrowsForbiddenException_BeforeQuotaOrGameSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var nonCreatorId = Guid.NewGuid(); // different from creatorId

        var session = LiveGameSession.Create(sessionId, creatorId, "Catan", gameId: Guid.NewGuid());
        session.AddPlayer(null, "Creator Player", PlayerColor.Red, TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var handler = new StartLiveSessionCommandHandler(
            _repositoryMock.Object,
            _gameSessionRepositoryMock.Object,
            _quotaServiceMock.Object,
            _timeProvider,
            _unitOfWorkMock.Object);

        // Command issued by nonCreatorId (participant but not creator)
        var command = new StartLiveSessionCommand(sessionId, nonCreatorId, DefaultUserTier, DefaultUserRole);

        // Act & Assert — must throw before reaching quota or GameSession creation
        var act = () => handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ForbiddenException>();

        // Quota service must NOT have been consulted
        _quotaServiceMock.Verify(
            q => q.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UserTier>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()),
            Times.Never,
            because: "the creator guard fires before the quota block");

        // No GameSession must have been created
        _gameSessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never,
            because: "the creator guard fires before any GameSession correlation");

        // Session must NOT have been started
        session.Status.Should().NotBe(LiveSessionStatus.InProgress);

        // Repository UpdateAsync must NOT have been called
        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "#2608: Zero active players (GameId-backed) → ValidationException before GameSession ctor")]
    public async Task StartLiveSession_ZeroActivePlayers_GameIdBacked_ThrowsValidationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Session created by DefaultUserId with a GameId but all players removed
        var session = LiveGameSession.Create(sessionId, DefaultUserId, "Catan", gameId: gameId);
        var p1 = session.AddPlayer(null, "Player One", PlayerColor.Red, TimeProvider.System);
        session.RemovePlayer(p1.Id, TimeProvider.System); // deactivates the only player
        SetupRepoGetById(sessionId, session);

        var handler = new StartLiveSessionCommandHandler(
            _repositoryMock.Object,
            _gameSessionRepositoryMock.Object,
            _quotaServiceMock.Object,
            _timeProvider,
            _unitOfWorkMock.Object);

        // Creator starts — passes creator guard, hits zero-players guard
        var command = new StartLiveSessionCommand(sessionId, DefaultUserId, DefaultUserTier, DefaultUserRole);

        // Act & Assert
        var act = () => handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ValidationException>(
            because: "starting with zero active players is an intentional validation error (400), not a generic ctor failure");

        // No GameSession must have been created
        _gameSessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    // =============================================================================
    // region PauseLiveSessionCommandHandler
    // =============================================================================

    #region PauseLiveSessionCommandHandler

    [Fact]
    public async Task PauseLiveSession_HappyPath_PausesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = new PauseLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new PauseLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.Status.Should().Be(LiveSessionStatus.Paused);
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PauseLiveSession_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new PauseLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new PauseLiveSessionCommand(sessionId);

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task PauseLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new PauseLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region ResumeLiveSessionCommandHandler
    // =============================================================================

    #region ResumeLiveSessionCommandHandler

    [Fact]
    public async Task ResumeLiveSession_HappyPath_ResumesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionPaused(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = new ResumeLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new ResumeLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.Status.Should().Be(LiveSessionStatus.InProgress);
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResumeLiveSession_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new ResumeLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new ResumeLiveSessionCommand(sessionId);

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task ResumeLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ResumeLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region CompleteLiveSessionCommandHandler
    // =============================================================================

    #region CompleteLiveSessionCommandHandler

    // Helper: build a CompleteLiveSessionCommandHandler with both repositories injected.
    private CompleteLiveSessionCommandHandler BuildCompleteHandler() =>
        new(_repositoryMock.Object, _gameSessionRepositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

    [Fact]
    public async Task CompleteLiveSession_HappyPath_CompletesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = BuildCompleteHandler();
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.Status.Should().Be(LiveSessionStatus.Completed);
        session.CompletedAt.Should().NotBeNull();
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteLiveSession_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = BuildCompleteHandler();
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task CompleteLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = BuildCompleteHandler();

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ─── Issue #2587 Slice 1 T3: Lifecycle-sync tests ─────────────────────────────

    [Fact(DisplayName = "#2587-T3a: Complete with CorrelatedGameSessionId → GameSession loaded, completed, one SaveChanges")]
    public async Task CompleteLiveSession_WithCorrelatedGameSession_CompletesGameSessionAndSavesOnce()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var correlatedId = Guid.NewGuid();

        // LiveGameSession that has a correlated GameSession
        var session = LiveGameSession.Create(
            sessionId, DefaultUserId, "Catan",
            gameId: gameId,
            correlatedGameSessionId: correlatedId);
        session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        session.Start(TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        // Correlated GameSession in Setup state (as created by T2)
        var gameSession = new GameSession(correlatedId, gameId,
            new[] { new SessionPlayer("Alice", 1) }, DefaultUserId);
        _gameSessionRepositoryMock
            .Setup(r => r.GetByIdAsync(correlatedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameSession);

        GameSession? updatedGameSession = null;
        _gameSessionRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()))
            .Callback<GameSession, CancellationToken>((gs, _) => updatedGameSession = gs)
            .Returns(Task.CompletedTask);

        var handler = BuildCompleteHandler();
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — LiveGameSession completed
        session.Status.Should().Be(LiveSessionStatus.Completed);

        // Assert — correlated GameSession loaded and completed
        _gameSessionRepositoryMock.Verify(
            r => r.GetByIdAsync(correlatedId, It.IsAny<CancellationToken>()),
            Times.Once);
        _gameSessionRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Once);
        updatedGameSession.Should().NotBeNull();
        updatedGameSession!.Status.Should().Be(SessionStatus.Completed);

        // Assert — event-free bookkeeping: MarkCorrelatedComplete must NOT raise
        // GameSessionStartedEvent or GameSessionCompletedEvent (spurious audit + contributor
        // credits via SessionCompletedForContributorsHandler — controller review finding).
        updatedGameSession.DomainEvents.Should().NotContain(
            e => e is Api.BoundedContexts.GameManagement.Domain.Events.GameSessionStartedEvent,
            because: "shadow correlated complete must not fire GameSessionStartedEvent");
        updatedGameSession.DomainEvents.Should().NotContain(
            e => e is Api.BoundedContexts.GameManagement.Domain.Events.GameSessionCompletedEvent,
            because: "shadow correlated complete must not fire GameSessionCompletedEvent");

        // Assert — single SaveChanges commits both
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "#2587-T3b: Free-form session (CorrelatedGameSessionId == null) → no GameSession repo call")]
    public async Task CompleteLiveSession_FreeForm_NoGameSessionRepoCall()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId); // no GameId, no correlation
        SetupRepoGetById(sessionId, session);

        var handler = BuildCompleteHandler();
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — LiveGameSession completed
        session.Status.Should().Be(LiveSessionStatus.Completed);

        // Assert — GameSession repo never touched
        _gameSessionRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gameSessionRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — still one SaveChanges
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "#2587-T3c: Correlated GameSession already Completed → idempotent, no double-complete")]
    public async Task CompleteLiveSession_CorrelatedGameSessionAlreadyCompleted_NoDoubleComplete()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var correlatedId = Guid.NewGuid();

        var session = LiveGameSession.Create(
            sessionId, DefaultUserId, "Catan",
            gameId: gameId,
            correlatedGameSessionId: correlatedId);
        session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        session.Start(TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        // GameSession already in terminal Completed state
        var gameSession = new GameSession(correlatedId, gameId,
            new[] { new SessionPlayer("Alice", 1) }, DefaultUserId);
        gameSession.Start();
        gameSession.Complete();

        _gameSessionRepositoryMock
            .Setup(r => r.GetByIdAsync(correlatedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameSession);

        var handler = BuildCompleteHandler();
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act — must not throw (idempotent)
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — UpdateAsync never called on the already-completed GameSession
        _gameSessionRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — LiveGameSession itself is completed
        session.Status.Should().Be(LiveSessionStatus.Completed);

        // Assert — single SaveChanges
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Issue #2587 Slice 2: On-complete backfill for legacy sessions ─────────────

    [Fact(DisplayName = "#2587-S2-T1: Legacy backfill — GameId-backed + no correlation + active players → GameSession added, Completed, correlated, single SaveChanges")]
    public async Task CompleteLiveSession_LegacyBackfill_AddsCompletedGameSessionAndCorrelates()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Legacy session: has GameId but no CorrelatedGameSessionId
        var session = LiveGameSession.Create(sessionId, DefaultUserId, "Mage Knight", gameId: gameId);
        session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        session.AddPlayer(null, "Bob", PlayerColor.Blue, TimeProvider.System);
        session.Start(TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        GameSession? capturedGameSession = null;
        _gameSessionRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()))
            .Callback<GameSession, CancellationToken>((gs, _) => capturedGameSession = gs)
            .Returns(Task.CompletedTask);

        var handler = BuildCompleteHandler();
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — LiveGameSession completed
        session.Status.Should().Be(LiveSessionStatus.Completed);

        // Assert — GameSession was added via AddAsync (not UpdateAsync)
        _gameSessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _gameSessionRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — captured GameSession is in Completed state
        capturedGameSession.Should().NotBeNull();
        capturedGameSession!.Status.Should().Be(SessionStatus.Completed);
        capturedGameSession.GameId.Should().Be(gameId);
        capturedGameSession.CreatedByUserId.Should().Be(DefaultUserId);
        capturedGameSession.Players.Should().HaveCount(2);

        // Assert — event-free: MarkCorrelatedComplete must NOT raise domain events
        capturedGameSession.DomainEvents.Should().NotContain(
            e => e is Api.BoundedContexts.GameManagement.Domain.Events.GameSessionStartedEvent,
            because: "backfill shadow must not fire GameSessionStartedEvent");
        capturedGameSession.DomainEvents.Should().NotContain(
            e => e is Api.BoundedContexts.GameManagement.Domain.Events.GameSessionCompletedEvent,
            because: "backfill shadow must not fire GameSessionCompletedEvent");

        // Assert — correlation set on the live session
        session.CorrelatedGameSessionId.Should().Be(capturedGameSession.Id);

        // Assert — single SaveChanges commits both atomically
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "#2587-S2-T2: Already-correlated (Slice 1 path) — no new GameSession added, existing one completed")]
    public async Task CompleteLiveSession_AlreadyCorrelated_NoNewGameSessionAdded_Regression()
    {
        // Arrange (mirrors T3a to confirm it still passes after Slice 2 changes)
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var correlatedId = Guid.NewGuid();

        var session = LiveGameSession.Create(
            sessionId, DefaultUserId, "Catan",
            gameId: gameId,
            correlatedGameSessionId: correlatedId);
        session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        session.Start(TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var gameSession = new GameSession(correlatedId, gameId,
            new[] { new SessionPlayer("Alice", 1) }, DefaultUserId);
        _gameSessionRepositoryMock
            .Setup(r => r.GetByIdAsync(correlatedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameSession);

        var handler = BuildCompleteHandler();
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — no NEW GameSession created
        _gameSessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — existing GameSession was loaded and completed (UpdateAsync)
        _gameSessionRepositoryMock.Verify(
            r => r.GetByIdAsync(correlatedId, It.IsAny<CancellationToken>()),
            Times.Once);
        _gameSessionRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Once);

        session.Status.Should().Be(LiveSessionStatus.Completed);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "#2587-S2-T3: Free-form (GameId==null) — no GameSession created, live session completes")]
    public async Task CompleteLiveSession_FreeFormNoGameId_NoGameSessionCreated()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId); // GameId == null
        SetupRepoGetById(sessionId, session);

        var handler = BuildCompleteHandler();
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — no GameSession repo interaction at all
        _gameSessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gameSessionRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gameSessionRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        session.Status.Should().Be(LiveSessionStatus.Completed);
        session.CorrelatedGameSessionId.Should().BeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "#2587-S2-T4: Legacy with zero active players — no GameSession, no throw, live session still completes")]
    public async Task CompleteLiveSession_LegacyBackfill_ZeroActivePlayers_SkipsBackfillWithoutThrowing()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Session with a GameId but all players removed/deactivated
        var session = LiveGameSession.Create(sessionId, DefaultUserId, "Mage Knight", gameId: gameId);
        var p1 = session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        session.Start(TimeProvider.System);
        // Remove the only active player (deactivates it)
        session.RemovePlayer(p1.Id, TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var handler = BuildCompleteHandler();
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act — must NOT throw even though no active players
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — no GameSession added (guard kicked in)
        _gameSessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — live session still completed
        session.Status.Should().Be(LiveSessionStatus.Completed);
        session.CorrelatedGameSessionId.Should().BeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Issue #2587 Slice 1 T3 Part B: active-player filter in Start ─────────────

    [Fact(DisplayName = "#2587-T3-partB: Inactive player excluded from correlated GameSession player list")]
    public async Task StartLiveSession_InactivePlayerExcluded_FromCorrelatedGameSessionPlayers()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var session = LiveGameSession.Create(sessionId, DefaultUserId, "Catan", gameId: gameId);

        // Add two players: one will be removed (deactivated), one stays active
        var removedPlayer = session.AddPlayer(null, "Removed Player", PlayerColor.Red, TimeProvider.System);
        session.AddPlayer(null, "Active Player", PlayerColor.Blue, TimeProvider.System);
        // Simulate RemovePlayer which deactivates the player
        session.RemovePlayer(removedPlayer.Id, TimeProvider.System);

        SetupRepoGetById(sessionId, session);

        GameSession? capturedGameSession = null;
        _gameSessionRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()))
            .Callback<GameSession, CancellationToken>((gs, _) => capturedGameSession = gs)
            .Returns(Task.CompletedTask);

        var handler = new StartLiveSessionCommandHandler(
            _repositoryMock.Object,
            _gameSessionRepositoryMock.Object,
            _quotaServiceMock.Object,
            _timeProvider,
            _unitOfWorkMock.Object);
        var command = new StartLiveSessionCommand(sessionId, DefaultUserId, DefaultUserTier, DefaultUserRole);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — only the active player ("Active Player") maps to the GameSession
        capturedGameSession.Should().NotBeNull();
        capturedGameSession!.Players.Should().ContainSingle(
            because: "the inactive/removed player must be excluded by the .Where(p => p.IsActive) filter");
        capturedGameSession.Players[0].PlayerName.Should().Be("Active Player");
    }

    #endregion

    // =============================================================================
    // region SaveLiveSessionCommandHandler
    // =============================================================================

    #region SaveLiveSessionCommandHandler

    [Fact]
    public async Task SaveLiveSession_HappyPath_InProgress_SavesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = new SaveLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new SaveLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.LastSavedAt.Should().NotBeNull();
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveLiveSession_HappyPath_Paused_SavesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionPaused(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = new SaveLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new SaveLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.LastSavedAt.Should().NotBeNull();
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveLiveSession_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new SaveLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new SaveLiveSessionCommand(sessionId);

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task SaveLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new SaveLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region AddPlayerToLiveSessionCommandHandler
    // =============================================================================

    #region AddPlayerToLiveSessionCommandHandler

    [Fact]
    public async Task AddPlayer_HappyPath_ReturnsPlayerId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInCreatedStatus(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = new AddPlayerToLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new AddPlayerToLiveSessionCommand(
            sessionId,
            "Alice",
            PlayerColor.Blue);

        // Act
        var playerId = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        playerId.Should().NotBe(Guid.Empty);
        session.Players.Should().ContainSingle();
        session.Players[0].DisplayName.Should().Be("Alice");
        session.Players[0].Color.Should().Be(PlayerColor.Blue);
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddPlayer_WithOptionalParams_SetsCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInCreatedStatus(sessionId);
        // Add a first player so the second one is not auto-assigned Host
        session.AddPlayer(null, "First Player", PlayerColor.Red, TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var userId = Guid.NewGuid();
        var handler = new AddPlayerToLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new AddPlayerToLiveSessionCommand(
            sessionId,
            "Bob",
            PlayerColor.Green,
            UserId: userId,
            Role: PlayerRole.Spectator,
            AvatarUrl: "https://example.com/avatar.png");

        // Act
        var playerId = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        playerId.Should().NotBe(Guid.Empty);
        var addedPlayer = session.Players.First(p => p.Id == playerId);
        addedPlayer.DisplayName.Should().Be("Bob");
        addedPlayer.Color.Should().Be(PlayerColor.Green);
        addedPlayer.UserId.Should().Be(userId);
        addedPlayer.Role.Should().Be(PlayerRole.Spectator);
    }

    [Fact]
    public async Task AddPlayer_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new AddPlayerToLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new AddPlayerToLiveSessionCommand(sessionId, "Alice", PlayerColor.Red);

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task AddPlayer_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new AddPlayerToLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region RemovePlayerFromLiveSessionCommandHandler
    // =============================================================================

    #region RemovePlayerFromLiveSessionCommandHandler

    [Fact]
    public async Task RemovePlayer_HappyPath_RemovesPlayer()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInCreatedStatus(sessionId);
        var player = session.AddPlayer(null, "Solo Player", PlayerColor.Red, TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var handler = new RemovePlayerFromLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new RemovePlayerFromLiveSessionCommand(sessionId, player.Id);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        (session.HasPlayers).Should().BeFalse();
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemovePlayer_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new RemovePlayerFromLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new RemovePlayerFromLiveSessionCommand(sessionId, Guid.NewGuid());

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task RemovePlayer_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new RemovePlayerFromLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region UpdatePlayerOrderCommandHandler
    // =============================================================================

    #region UpdatePlayerOrderCommandHandler

    [Fact]
    public async Task UpdatePlayerOrder_HappyPath_SetsTurnOrder()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInCreatedStatus(sessionId);
        var player1 = session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        var player2 = session.AddPlayer(null, "Bob", PlayerColor.Blue, TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var handler = new UpdatePlayerOrderCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        // Reverse the order
        var command = new UpdatePlayerOrderCommand(sessionId, new List<Guid> { player2.Id, player1.Id });

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.TurnOrder[0].Should().Be(player2.Id);
        session.TurnOrder[1].Should().Be(player1.Id);
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePlayerOrder_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new UpdatePlayerOrderCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new UpdatePlayerOrderCommand(sessionId, new List<Guid> { Guid.NewGuid() });

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task UpdatePlayerOrder_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new UpdatePlayerOrderCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region CreateLiveSessionTeamCommandHandler
    // =============================================================================

    #region CreateLiveSessionTeamCommandHandler

    [Fact]
    public async Task CreateTeam_HappyPath_ReturnsTeamId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInCreatedStatus(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = new CreateLiveSessionTeamCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new CreateLiveSessionTeamCommand(sessionId, "Red Team", "#FF0000");

        // Act
        var teamId = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        teamId.Should().NotBe(Guid.Empty);
        session.Teams.Should().ContainSingle();
        session.Teams[0].Name.Should().Be("Red Team");
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTeam_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new CreateLiveSessionTeamCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new CreateLiveSessionTeamCommand(sessionId, "Team A", "#00FF00");

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task CreateTeam_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new CreateLiveSessionTeamCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region AssignPlayerToTeamCommandHandler
    // =============================================================================

    #region AssignPlayerToTeamCommandHandler

    [Fact]
    public async Task AssignPlayerToTeam_HappyPath_AssignsCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInCreatedStatus(sessionId);
        var player = session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        var team = session.CreateTeam("Alpha", "#FF0000", TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var handler = new AssignPlayerToTeamCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new AssignPlayerToTeamCommand(sessionId, player.Id, team.Id);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.Players[0].TeamId.Should().Be(team.Id);
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AssignPlayerToTeam_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new AssignPlayerToTeamCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new AssignPlayerToTeamCommand(sessionId, Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task AssignPlayerToTeam_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new AssignPlayerToTeamCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region RecordLiveSessionScoreCommandHandler
    // =============================================================================

    #region RecordLiveSessionScoreCommandHandler

    [Fact]
    public async Task RecordScore_HappyPath_RecordsScore()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        var playerId = session.Players[0].Id;
        SetupRepoGetById(sessionId, session);

        var handler = new RecordLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new RecordLiveSessionScoreCommand(sessionId, playerId, Round: 1, Dimension: "Points", Value: 10);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.RoundScores.Should().ContainSingle();
        session.RoundScores[0].Value.Should().Be(10);
        session.RoundScores[0].Dimension.Should().Be("Points");
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordScore_WithUnit_RecordsScoreWithUnit()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        var playerId = session.Players[0].Id;
        SetupRepoGetById(sessionId, session);

        var handler = new RecordLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new RecordLiveSessionScoreCommand(sessionId, playerId, Round: 1, Dimension: "Points", Value: 25, Unit: "pts");

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.RoundScores.Should().ContainSingle();
        session.RoundScores[0].Unit.Should().Be("pts");
    }

    [Fact]
    public async Task RecordScore_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new RecordLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new RecordLiveSessionScoreCommand(sessionId, Guid.NewGuid(), 1, "Points", 10);

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task RecordScore_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new RecordLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region EditLiveSessionScoreCommandHandler
    // =============================================================================

    #region EditLiveSessionScoreCommandHandler

    [Fact]
    public async Task EditScore_HappyPath_UpsertsBehavior()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        var playerId = session.Players[0].Id;
        // Record an initial score
        session.RecordScore(playerId, 1, "Points", 10, TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var handler = new EditLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new EditLiveSessionScoreCommand(sessionId, playerId, Round: 1, Dimension: "Points", Value: 20);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - should replace the existing score (upsert)
        session.RoundScores.Where(s => s.Round == 1 && s.Dimension == "Points").Should().ContainSingle();
        var updatedScore = session.RoundScores.Single(s => s.Round == 1 && s.Dimension == "Points");
        updatedScore.Value.Should().Be(20);
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EditScore_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new EditLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new EditLiveSessionScoreCommand(sessionId, Guid.NewGuid(), 1, "Points", 10);

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task EditScore_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new EditLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region AdvanceLiveSessionTurnCommandHandler
    // =============================================================================

    #region AdvanceLiveSessionTurnCommandHandler

    [Fact]
    public async Task AdvanceTurn_HappyPath_AdvancesTurn()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        var initialTurn = session.CurrentTurnIndex;
        SetupRepoGetById(sessionId, session);

        var handler = new AdvanceLiveSessionTurnCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new AdvanceLiveSessionTurnCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.CurrentTurnIndex.Should().Be(initialTurn + 1);
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdvanceTurn_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new AdvanceLiveSessionTurnCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new AdvanceLiveSessionTurnCommand(sessionId);

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task AdvanceTurn_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new AdvanceLiveSessionTurnCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region UpdateLiveSessionNotesCommandHandler
    // =============================================================================

    #region UpdateLiveSessionNotesCommandHandler

    [Fact]
    public async Task UpdateNotes_HappyPath_UpdatesNotes()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInCreatedStatus(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = new UpdateLiveSessionNotesCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new UpdateLiveSessionNotesCommand(sessionId, "Game is going well!");

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.Notes.Should().Be("Game is going well!");
        _repositoryMock.Verify(
            r => r.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateNotes_NullNotes_ClearsNotes()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInCreatedStatus(sessionId);
        session.UpdateNotes("Previous notes", TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var handler = new UpdateLiveSessionNotesCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new UpdateLiveSessionNotesCommand(sessionId, null);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.Notes.Should().BeNull();
    }

    [Fact]
    public async Task UpdateNotes_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new UpdateLiveSessionNotesCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new UpdateLiveSessionNotesCommand(sessionId, "Notes");

        // Act & Assert
        var act =
            () => handler.Handle(command, TestContext.Current.CancellationToken);
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task UpdateNotes_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new UpdateLiveSessionNotesCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object);

        // Act & Assert
        var act =
            () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    // =============================================================================
    // region CancellationToken Propagation
    // =============================================================================

    #region CancellationToken Propagation

    [Fact]
    public async Task CreateLiveSession_PassesCancellationTokenToRepository()
    {
        // Arrange
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider, _unitOfWorkMock.Object, _companionSessionServiceMock.Object);
        var command = new CreateLiveSessionCommand(DefaultUserId, "Token Test Game");
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act
        await handler.Handle(command, ct);

        // Assert
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<LiveGameSession>(), ct),
            Times.Once);
    }

    [Fact]
    public async Task StartLiveSession_PassesCancellationTokenToRepository()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithPlayer(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = new StartLiveSessionCommandHandler(_repositoryMock.Object, _gameSessionRepositoryMock.Object, _quotaServiceMock.Object, _timeProvider, _unitOfWorkMock.Object);
        var command = new StartLiveSessionCommand(sessionId, DefaultUserId, DefaultUserTier, DefaultUserRole);
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act
        await handler.Handle(command, ct);

        // Assert
        _repositoryMock.Verify(r => r.GetByIdAsync(sessionId, ct), Times.Once);
        _repositoryMock.Verify(r => r.UpdateAsync(session, ct), Times.Once);
    }

    // ─── Issue #2587 Slice 1: GameSession correlation + quota enforcement ───

    [Fact(DisplayName = "#2587-a: GameId-backed session — quota checked, GameSession created, correlated, one SaveChanges")]
    public async Task StartLiveSession_GameIdBacked_CreatesCorrelatedGameSessionAndChecksQuota()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = LiveGameSession.Create(sessionId, DefaultUserId, "Catan", gameId: gameId);
        session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        GameSession? capturedGameSession = null;
        _gameSessionRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()))
            .Callback<GameSession, CancellationToken>((gs, _) => capturedGameSession = gs)
            .Returns(Task.CompletedTask);

        var handler = new StartLiveSessionCommandHandler(
            _repositoryMock.Object,
            _gameSessionRepositoryMock.Object,
            _quotaServiceMock.Object,
            _timeProvider,
            _unitOfWorkMock.Object);
        var command = new StartLiveSessionCommand(sessionId, DefaultUserId, DefaultUserTier, DefaultUserRole);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — quota was checked
        _quotaServiceMock.Verify(
            q => q.CheckQuotaAsync(DefaultUserId, DefaultUserTier, DefaultUserRole, It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — GameSession was added with correct gameId + createdByUserId
        _gameSessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Once);
        capturedGameSession.Should().NotBeNull();
        capturedGameSession!.GameId.Should().Be(gameId);
        capturedGameSession.CreatedByUserId.Should().Be(DefaultUserId);

        // Assert — correlation set
        session.CorrelatedGameSessionId.Should().Be(capturedGameSession.Id);

        // Assert — session started
        session.Status.Should().Be(LiveSessionStatus.InProgress);

        // Assert — single SaveChanges
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "#2587-b: Quota denied — throws QuotaExceededException, no GameSession added, no Start")]
    public async Task StartLiveSession_QuotaDenied_ThrowsQuotaExceededAndDoesNotCreateGameSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var session = LiveGameSession.Create(sessionId, DefaultUserId, "Catan", gameId: gameId);
        session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        _quotaServiceMock
            .Setup(q => q.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UserTier>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Denied("Limit reached", currentCount: 3, maxAllowed: 3));

        var handler = new StartLiveSessionCommandHandler(
            _repositoryMock.Object,
            _gameSessionRepositoryMock.Object,
            _quotaServiceMock.Object,
            _timeProvider,
            _unitOfWorkMock.Object);
        var command = new StartLiveSessionCommand(sessionId, DefaultUserId, DefaultUserTier, DefaultUserRole);

        // Act & Assert
        await Assert.ThrowsAsync<QuotaExceededException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));

        _gameSessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        session.Status.Should().Be(LiveSessionStatus.Created);
    }

    [Fact(DisplayName = "#2587-c: Already-correlated — no second GameSession, idempotent Start")]
    public async Task StartLiveSession_AlreadyCorrelated_SkipsGameSessionCreation()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var existingCorrelationId = Guid.NewGuid();
        var session = LiveGameSession.Create(
            sessionId, DefaultUserId, "Catan",
            gameId: gameId,
            correlatedGameSessionId: existingCorrelationId);
        session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var handler = new StartLiveSessionCommandHandler(
            _repositoryMock.Object,
            _gameSessionRepositoryMock.Object,
            _quotaServiceMock.Object,
            _timeProvider,
            _unitOfWorkMock.Object);
        var command = new StartLiveSessionCommand(sessionId, DefaultUserId, DefaultUserTier, DefaultUserRole);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — AddAsync never called
        _gameSessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — quota check skipped (already correlated guard short-circuits)
        _quotaServiceMock.Verify(
            q => q.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UserTier>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — correlation unchanged
        session.CorrelatedGameSessionId.Should().Be(existingCorrelationId);
        session.Status.Should().Be(LiveSessionStatus.InProgress);
    }

    [Fact(DisplayName = "#2587-d: Free-form (GameId==null) — no quota check, no GameSession, session starts")]
    public async Task StartLiveSession_FreeForm_SkipsQuotaAndGameSessionCreation()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = LiveGameSession.Create(sessionId, DefaultUserId, "Free Play"); // GameId = null
        session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);
        SetupRepoGetById(sessionId, session);

        var handler = new StartLiveSessionCommandHandler(
            _repositoryMock.Object,
            _gameSessionRepositoryMock.Object,
            _quotaServiceMock.Object,
            _timeProvider,
            _unitOfWorkMock.Object);
        var command = new StartLiveSessionCommand(sessionId, DefaultUserId, DefaultUserTier, DefaultUserRole);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _quotaServiceMock.Verify(
            q => q.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UserTier>(), It.IsAny<Role>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gameSessionRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        session.Status.Should().Be(LiveSessionStatus.InProgress);
        session.CorrelatedGameSessionId.Should().BeNull();
    }

    [Fact(DisplayName = "#2587-e: DbUpdateConcurrencyException — re-fetch shows correlated → idempotent success")]
    public async Task StartLiveSession_ConcurrencyException_RefetchShowsCorrelated_ReturnsIdempotently()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // First GetByIdAsync: returns a fresh Created session (not yet correlated)
        var session = LiveGameSession.Create(sessionId, DefaultUserId, "Catan", gameId: gameId);
        session.AddPlayer(null, "Alice", PlayerColor.Red, TimeProvider.System);

        // Re-fetch (after catch): returns a session with correlation already set by the "winner"
        var correlationId = Guid.NewGuid();
        var refreshedSession = LiveGameSession.Create(
            sessionId, DefaultUserId, "Catan",
            gameId: gameId,
            correlatedGameSessionId: correlationId);

        var callCount = 0;
        _repositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? session : refreshedSession;
            });

        // SaveChanges throws concurrency exception on the first (and only) attempt
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var handler = new StartLiveSessionCommandHandler(
            _repositoryMock.Object,
            _gameSessionRepositoryMock.Object,
            _quotaServiceMock.Object,
            _timeProvider,
            _unitOfWorkMock.Object);
        var command = new StartLiveSessionCommand(sessionId, DefaultUserId, DefaultUserTier, DefaultUserRole);

        // Act — must not throw (idempotent success)
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — SaveChanges called exactly once (no retry loop)
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
