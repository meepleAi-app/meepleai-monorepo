using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
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
    private readonly TimeProvider _timeProvider;

    private static readonly Guid DefaultUserId = Guid.NewGuid();

    public LiveSessionCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionRepository>();
        _timeProvider = TimeProvider.System;
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
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
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
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
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
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateLiveSession_VerifiesAddAsyncCalled()
    {
        // Arrange
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new CreateLiveSessionCommand(DefaultUserId, "Ticket to Ride");

        LiveGameSession? capturedSession = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LiveGameSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveGameSession, CancellationToken>((s, _) => capturedSession = s)
            .Returns(Task.CompletedTask);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedSession);
        capturedSession.Id.Should().Be(result);
        capturedSession.GameName.Should().Be("Ticket to Ride");
        capturedSession.CreatedByUserId.Should().Be(DefaultUserId);
    }

    [Fact]
    public async Task CreateLiveSession_WithAllOptionalParams_CreatesCorrectly()
    {
        // Arrange
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var gameId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
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
        Assert.NotNull(capturedSession);
        capturedSession.GameId.Should().Be(gameId);
        capturedSession.Visibility.Should().Be(PlayRecordVisibility.Group);
        capturedSession.GroupId.Should().Be(groupId);
        capturedSession.AgentMode.Should().Be(AgentSessionMode.Assistant);
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

        var handler = new StartLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new StartLiveSessionCommand(sessionId);

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

        var handler = new StartLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new StartLiveSessionCommand(sessionId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task StartLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new StartLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new PauseLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new PauseLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new PauseLiveSessionCommand(sessionId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task PauseLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new PauseLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new ResumeLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new ResumeLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new ResumeLiveSessionCommand(sessionId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task ResumeLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ResumeLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    #endregion

    // =============================================================================
    // region CompleteLiveSessionCommandHandler
    // =============================================================================

    #region CompleteLiveSessionCommandHandler

    [Fact]
    public async Task CompleteLiveSession_HappyPath_CompletesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = CreateSessionInProgress(sessionId);
        SetupRepoGetById(sessionId, session);

        var handler = new CompleteLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        session.Status.Should().Be(LiveSessionStatus.Completed);
        Assert.NotNull(session.CompletedAt);
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

        var handler = new CompleteLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new CompleteLiveSessionCommand(sessionId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task CompleteLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new CompleteLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new SaveLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new SaveLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(session.LastSavedAt);
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

        var handler = new SaveLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new SaveLiveSessionCommand(sessionId);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(session.LastSavedAt);
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

        var handler = new SaveLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new SaveLiveSessionCommand(sessionId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task SaveLiveSession_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new SaveLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new AddPlayerToLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
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
        var handler = new AddPlayerToLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new AddPlayerToLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new AddPlayerToLiveSessionCommand(sessionId, "Alice", PlayerColor.Red);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task AddPlayer_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new AddPlayerToLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new RemovePlayerFromLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new RemovePlayerFromLiveSessionCommand(sessionId, player.Id);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(session.HasPlayers);
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

        var handler = new RemovePlayerFromLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new RemovePlayerFromLiveSessionCommand(sessionId, Guid.NewGuid());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task RemovePlayer_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new RemovePlayerFromLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new UpdatePlayerOrderCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new UpdatePlayerOrderCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new UpdatePlayerOrderCommand(sessionId, new List<Guid> { Guid.NewGuid() });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task UpdatePlayerOrder_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new UpdatePlayerOrderCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new CreateLiveSessionTeamCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new CreateLiveSessionTeamCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new CreateLiveSessionTeamCommand(sessionId, "Team A", "#00FF00");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task CreateTeam_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new CreateLiveSessionTeamCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new AssignPlayerToTeamCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new AssignPlayerToTeamCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new AssignPlayerToTeamCommand(sessionId, Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task AssignPlayerToTeam_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new AssignPlayerToTeamCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new RecordLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new RecordLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new RecordLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new RecordLiveSessionScoreCommand(sessionId, Guid.NewGuid(), 1, "Points", 10);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task RecordScore_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new RecordLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new EditLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new EditLiveSessionScoreCommand(sessionId, playerId, Round: 1, Dimension: "Points", Value: 20);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - should replace the existing score (upsert)
        var updatedScore = Assert.Single(session.RoundScores, s => s.Round == 1 && s.Dimension == "Points");
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

        var handler = new EditLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new EditLiveSessionScoreCommand(sessionId, Guid.NewGuid(), 1, "Points", 10);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task EditScore_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new EditLiveSessionScoreCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new AdvanceLiveSessionTurnCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new AdvanceLiveSessionTurnCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new AdvanceLiveSessionTurnCommand(sessionId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task AdvanceTurn_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new AdvanceLiveSessionTurnCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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

        var handler = new UpdateLiveSessionNotesCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new UpdateLiveSessionNotesCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new UpdateLiveSessionNotesCommand(sessionId, null);

        // Act
        await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(session.Notes);
    }

    [Fact]
    public async Task UpdateNotes_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupRepoGetById(sessionId, null);

        var handler = new UpdateLiveSessionNotesCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new UpdateLiveSessionNotesCommand(sessionId, "Notes");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
        ex.ResourceType.Should().Be("LiveGameSession");
    }

    [Fact]
    public async Task UpdateNotes_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new UpdateLiveSessionNotesCommandHandler(_repositoryMock.Object, _timeProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestContext.Current.CancellationToken));
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
        var handler = new CreateLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
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

        var handler = new StartLiveSessionCommandHandler(_repositoryMock.Object, _timeProvider);
        var command = new StartLiveSessionCommand(sessionId);
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act
        await handler.Handle(command, ct);

        // Assert
        _repositoryMock.Verify(r => r.GetByIdAsync(sessionId, ct), Times.Once);
        _repositoryMock.Verify(r => r.UpdateAsync(session, ct), Times.Once);
    }

    #endregion
}
