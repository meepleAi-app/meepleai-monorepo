using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveGameSessionTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _now;

    public LiveGameSessionTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 19, 12, 0, 0, TimeSpan.Zero));
        _now = _timeProvider.GetUtcNow().UtcDateTime;
    }

    private LiveGameSession CreateDefaultSession(
        Guid? id = null,
        Guid? userId = null,
        string gameName = "Catan",
        Guid? gameId = null)
    {
        return LiveGameSession.Create(
            id ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            gameName,
            _timeProvider,
            gameId);
    }

    private LiveSessionPlayer AddDefaultPlayer(
        LiveGameSession session,
        string name = "Player 1",
        PlayerColor color = PlayerColor.Red,
        Guid? userId = null,
        PlayerRole? role = null)
    {
        return session.AddPlayer(userId, name, color, _timeProvider, role);
    }

    #region Create Factory Method

    [Fact]
    public void Create_ValidParameters_CreatesSuccessfully()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var session = LiveGameSession.Create(
            id, userId, "Catan", _timeProvider, gameId);

        // Assert
        Assert.Equal(id, session.Id);
        Assert.Equal(userId, session.CreatedByUserId);
        Assert.Equal("Catan", session.GameName);
        Assert.Equal(gameId, session.GameId);
        Assert.Equal(LiveSessionStatus.Created, session.Status);
        Assert.NotNull(session.SessionCode);
        Assert.Equal(6, session.SessionCode.Length);
        Assert.Equal(_now, session.CreatedAt);
        Assert.Equal(_now, session.UpdatedAt);
        Assert.Equal(0, session.CurrentTurnIndex);
        Assert.NotNull(session.ScoringConfig);
        Assert.Equal(AgentSessionMode.None, session.AgentMode);
        Assert.Null(session.StartedAt);
        Assert.Null(session.PausedAt);
        Assert.Null(session.CompletedAt);
        Assert.Empty(session.Players);
        Assert.Empty(session.Teams);
    }

    [Fact]
    public void Create_RaisesLiveSessionCreatedEvent()
    {
        // Act
        var session = CreateDefaultSession();

        // Assert
        Assert.Single(session.DomainEvents);
        var evt = Assert.IsType<LiveSessionCreatedEvent>(session.DomainEvents.First());
        Assert.Equal(session.Id, evt.SessionId);
        Assert.Equal(session.CreatedByUserId, evt.CreatedByUserId);
        Assert.Equal("Catan", evt.GameName);
    }

    [Fact]
    public void Create_WithGroupVisibility_RequiresGroupId()
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            LiveGameSession.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Catan",
                _timeProvider,
                visibility: PlayRecordVisibility.Group,
                groupId: null));
    }

    [Fact]
    public void Create_WithGroupVisibility_AndGroupId_Succeeds()
    {
        // Arrange
        var groupId = Guid.NewGuid();

        // Act
        var session = LiveGameSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Catan",
            _timeProvider,
            visibility: PlayRecordVisibility.Group,
            groupId: groupId);

        // Assert
        Assert.Equal(PlayRecordVisibility.Group, session.Visibility);
        Assert.Equal(groupId, session.GroupId);
    }

    [Fact]
    public void Create_EmptyId_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            LiveGameSession.Create(Guid.Empty, Guid.NewGuid(), "Catan", _timeProvider));
    }

    [Fact]
    public void Create_EmptyUserId_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            LiveGameSession.Create(Guid.NewGuid(), Guid.Empty, "Catan", _timeProvider));
    }

    [Fact]
    public void Create_EmptyGameName_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), "", _timeProvider));
    }

    [Fact]
    public void Create_GameNameTooLong_ThrowsValidationException()
    {
        var longName = new string('a', 256);
        Assert.Throws<ValidationException>(() =>
            LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), longName, _timeProvider));
    }

    [Fact]
    public void Create_WithCustomScoringConfig_UsesThat()
    {
        // Arrange
        var config = new SessionScoringConfig(new[] { "points", "coins" });

        // Act
        var session = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", _timeProvider,
            scoringConfig: config);

        // Assert
        Assert.Equal(2, session.ScoringConfig.EnabledDimensions.Count);
    }

    [Fact]
    public void Create_WithAgentMode_SetsMode()
    {
        // Act
        var session = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", _timeProvider,
            agentMode: AgentSessionMode.Assistant);

        // Assert
        Assert.Equal(AgentSessionMode.Assistant, session.AgentMode);
    }

    [Fact]
    public void Create_SessionCode_IsAlphanumericAndReadable()
    {
        // Create multiple sessions to check code generation
        for (var i = 0; i < 10; i++)
        {
            var session = CreateDefaultSession();
            Assert.Matches("^[A-Z2-9]{6}$", session.SessionCode);
            // Should not contain I, O, 0, 1 (confusing characters)
            Assert.DoesNotContain("I", session.SessionCode);
            Assert.DoesNotContain("O", session.SessionCode);
            Assert.DoesNotContain("0", session.SessionCode);
            Assert.DoesNotContain("1", session.SessionCode);
        }
    }

    #endregion

    #region Player Management

    [Fact]
    public void AddPlayer_FirstPlayer_AssignsHostRole()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        var player = AddDefaultPlayer(session);

        // Assert
        Assert.Equal(PlayerRole.Host, player.Role);
        Assert.Single(session.Players);
        Assert.True(session.HasPlayers);
        Assert.Equal(1, session.PlayerCount);
    }

    [Fact]
    public void AddPlayer_SecondPlayer_AssignsPlayerRole()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session, "Host", PlayerColor.Red);

        // Act
        var player = AddDefaultPlayer(session, "Player 2", PlayerColor.Blue);

        // Assert
        Assert.Equal(PlayerRole.Player, player.Role);
        Assert.Equal(2, session.Players.Count);
    }

    [Fact]
    public void AddPlayer_WithExplicitRole_UsesSpecifiedRole()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        var player = AddDefaultPlayer(session, role: PlayerRole.Spectator);

        // Assert
        Assert.Equal(PlayerRole.Spectator, player.Role);
    }

    [Fact]
    public void AddPlayer_RaisesPlayerAddedEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.ClearDomainEvents();

        // Act
        AddDefaultPlayer(session);

        // Assert
        Assert.Single(session.DomainEvents);
        Assert.IsType<LiveSessionPlayerAddedEvent>(session.DomainEvents.First());
    }

    [Fact]
    public void AddPlayer_DuplicateUserId_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var userId = Guid.NewGuid();
        AddDefaultPlayer(session, "Player 1", PlayerColor.Red, userId);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            AddDefaultPlayer(session, "Player 2", PlayerColor.Blue, userId));
    }

    [Fact]
    public void AddPlayer_DuplicateDisplayName_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session, "Marco", PlayerColor.Red);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            AddDefaultPlayer(session, "Marco", PlayerColor.Blue));
    }

    [Fact]
    public void AddPlayer_DuplicateColor_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session, "Player 1", PlayerColor.Red);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            AddDefaultPlayer(session, "Player 2", PlayerColor.Red));
    }

    [Fact]
    public void AddPlayer_ToCompletedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Complete(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            AddDefaultPlayer(session, "New Player", PlayerColor.Blue));
    }

    [Fact]
    public void AddPlayer_AddsToTurnOrder()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        var player = AddDefaultPlayer(session);

        // Assert
        Assert.Single(session.TurnOrder);
        Assert.Equal(player.Id, session.TurnOrder[0]);
    }

    [Fact]
    public void RemovePlayer_ExistingPlayer_Deactivates()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session, "Solo", PlayerColor.Red);

        // Act
        session.RemovePlayer(player.Id, _timeProvider);

        // Assert
        Assert.False(player.IsActive);
        Assert.Equal(0, session.PlayerCount);
    }

    [Fact]
    public void RemovePlayer_Host_WithOtherPlayers_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var host = AddDefaultPlayer(session, "Host", PlayerColor.Red);
        AddDefaultPlayer(session, "Player 2", PlayerColor.Blue);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            session.RemovePlayer(host.Id, _timeProvider));
    }

    [Fact]
    public void RemovePlayer_RaisesPlayerRemovedEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session, "Solo", PlayerColor.Red);
        session.ClearDomainEvents();

        // Act
        session.RemovePlayer(player.Id, _timeProvider);

        // Assert
        Assert.Single(session.DomainEvents);
        Assert.IsType<LiveSessionPlayerRemovedEvent>(session.DomainEvents.First());
    }

    [Fact]
    public void RemovePlayer_NonExistentPlayer_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            session.RemovePlayer(Guid.NewGuid(), _timeProvider));
    }

    [Fact]
    public void SetTurnOrder_ValidPlayerIds_UpdatesOrder()
    {
        // Arrange
        var session = CreateDefaultSession();
        var p1 = AddDefaultPlayer(session, "P1", PlayerColor.Red);
        var p2 = AddDefaultPlayer(session, "P2", PlayerColor.Blue);
        var p3 = AddDefaultPlayer(session, "P3", PlayerColor.Green);

        // Act
        session.SetTurnOrder(new List<Guid> { p3.Id, p1.Id, p2.Id }, _timeProvider);

        // Assert
        Assert.Equal(p3.Id, session.TurnOrder[0]);
        Assert.Equal(p1.Id, session.TurnOrder[1]);
        Assert.Equal(p2.Id, session.TurnOrder[2]);
    }

    [Fact]
    public void SetTurnOrder_InvalidPlayerId_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            session.SetTurnOrder(new List<Guid> { Guid.NewGuid() }, _timeProvider));
    }

    #endregion

    #region Team Management

    [Fact]
    public void CreateTeam_ValidParameters_CreatesTeam()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        var team = session.CreateTeam("Alpha", "#FF0000", _timeProvider);

        // Assert
        Assert.NotNull(team);
        Assert.Equal("Alpha", team.Name);
        Assert.Equal("#FF0000", team.Color);
        Assert.Single(session.Teams);
    }

    [Fact]
    public void CreateTeam_DuplicateName_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.CreateTeam("Alpha", "#FF0000", _timeProvider);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            session.CreateTeam("Alpha", "#0000FF", _timeProvider));
    }

    [Fact]
    public void AssignPlayerToTeam_ValidIds_AssignsPlayer()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        var team = session.CreateTeam("Alpha", "#FF0000", _timeProvider);

        // Act
        session.AssignPlayerToTeam(player.Id, team.Id, _timeProvider);

        // Assert
        Assert.Equal(team.Id, player.TeamId);
        Assert.Single(team.PlayerIds);
        Assert.Equal(player.Id, team.PlayerIds[0]);
    }

    [Fact]
    public void AssignPlayerToTeam_AlreadyOnTeam_MovesToNewTeam()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        var team1 = session.CreateTeam("Alpha", "#FF0000", _timeProvider);
        var team2 = session.CreateTeam("Beta", "#0000FF", _timeProvider);
        session.AssignPlayerToTeam(player.Id, team1.Id, _timeProvider);

        // Act
        session.AssignPlayerToTeam(player.Id, team2.Id, _timeProvider);

        // Assert
        Assert.Equal(team2.Id, player.TeamId);
        Assert.Empty(team1.PlayerIds);
        Assert.Single(team2.PlayerIds);
    }

    #endregion

    #region Lifecycle Management

    [Fact]
    public void MoveToSetup_FromCreated_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        session.MoveToSetup(_timeProvider);

        // Assert
        Assert.Equal(LiveSessionStatus.Setup, session.Status);
    }

    [Fact]
    public void MoveToSetup_FromInProgress_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() => session.MoveToSetup(_timeProvider));
    }

    [Fact]
    public void Start_FromCreated_WithPlayers_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);

        // Act
        session.Start(_timeProvider);

        // Assert
        Assert.Equal(LiveSessionStatus.InProgress, session.Status);
        Assert.Equal(_now, session.StartedAt);
        Assert.Equal(1, session.CurrentTurnIndex);
    }

    [Fact]
    public void Start_FromSetup_WithPlayers_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.MoveToSetup(_timeProvider);

        // Act
        session.Start(_timeProvider);

        // Assert
        Assert.Equal(LiveSessionStatus.InProgress, session.Status);
    }

    [Fact]
    public void Start_WithoutPlayers_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        Assert.Throws<ConflictException>(() => session.Start(_timeProvider));
    }

    [Fact]
    public void Start_FromPaused_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Pause(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() => session.Start(_timeProvider));
    }

    [Fact]
    public void Start_RaisesLiveSessionStartedEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.ClearDomainEvents();

        // Act
        session.Start(_timeProvider);

        // Assert
        Assert.Single(session.DomainEvents);
        var evt = Assert.IsType<LiveSessionStartedEvent>(session.DomainEvents.First());
        Assert.Equal(session.Id, evt.SessionId);
    }

    [Fact]
    public void Pause_FromInProgress_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Pause(_timeProvider);

        // Assert
        Assert.Equal(LiveSessionStatus.Paused, session.Status);
        Assert.Equal(_now, session.PausedAt);
        Assert.Single(session.DomainEvents);
        Assert.IsType<LiveSessionPausedEvent>(session.DomainEvents.First());
    }

    [Fact]
    public void Pause_FromSetup_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.MoveToSetup(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() => session.Pause(_timeProvider));
    }

    [Fact]
    public void Resume_FromPaused_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Pause(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Resume(_timeProvider);

        // Assert
        Assert.Equal(LiveSessionStatus.InProgress, session.Status);
        Assert.Null(session.PausedAt);
        Assert.Single(session.DomainEvents);
        Assert.IsType<LiveSessionResumedEvent>(session.DomainEvents.First());
    }

    [Fact]
    public void Resume_FromInProgress_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() => session.Resume(_timeProvider));
    }

    [Fact]
    public void Complete_FromInProgress_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Complete(_timeProvider);

        // Assert
        Assert.Equal(LiveSessionStatus.Completed, session.Status);
        Assert.Equal(_now, session.CompletedAt);
        Assert.Single(session.DomainEvents);
        var evt = Assert.IsType<LiveSessionCompletedEvent>(session.DomainEvents.First());
        Assert.Equal(1, evt.TotalTurns);
    }

    [Fact]
    public void Complete_FromPaused_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Pause(_timeProvider);

        // Act
        session.Complete(_timeProvider);

        // Assert
        Assert.Equal(LiveSessionStatus.Completed, session.Status);
    }

    [Fact]
    public void Complete_FromCreated_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        Assert.Throws<ConflictException>(() => session.Complete(_timeProvider));
    }

    [Fact]
    public void IsActive_ReturnsCorrectly()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Created is not "active" (no setup yet)
        Assert.False(session.IsActive);

        session.MoveToSetup(_timeProvider);
        Assert.True(session.IsActive);

        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        Assert.True(session.IsActive);

        session.Pause(_timeProvider);
        Assert.True(session.IsActive);

        session.Complete(_timeProvider);
        Assert.False(session.IsActive);
    }

    #endregion

    #region Scoring

    [Fact]
    public void RecordScore_ValidParameters_RecordsScore()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.RecordScore(player.Id, 1, "points", 10, _timeProvider);

        // Assert
        Assert.Single(session.RoundScores);
        Assert.Equal(10, session.RoundScores[0].Value);
        Assert.Equal(10, player.TotalScore);
        Assert.Equal(1, player.CurrentRank);
    }

    [Fact]
    public void RecordScore_MultiplePlayers_CalculatesRanks()
    {
        // Arrange
        var session = CreateDefaultSession();
        var p1 = AddDefaultPlayer(session, "P1", PlayerColor.Red);
        var p2 = AddDefaultPlayer(session, "P2", PlayerColor.Blue);
        session.Start(_timeProvider);

        // Act
        session.RecordScore(p1.Id, 1, "points", 5, _timeProvider);
        session.RecordScore(p2.Id, 1, "points", 10, _timeProvider);

        // Assert
        Assert.Equal(2, p1.CurrentRank);
        Assert.Equal(1, p2.CurrentRank);
    }

    [Fact]
    public void RecordScore_TiedScores_SameRank()
    {
        // Arrange
        var session = CreateDefaultSession();
        var p1 = AddDefaultPlayer(session, "P1", PlayerColor.Red);
        var p2 = AddDefaultPlayer(session, "P2", PlayerColor.Blue);
        session.Start(_timeProvider);

        // Act
        session.RecordScore(p1.Id, 1, "points", 10, _timeProvider);
        session.RecordScore(p2.Id, 1, "points", 10, _timeProvider);

        // Assert
        Assert.Equal(1, p1.CurrentRank);
        Assert.Equal(1, p2.CurrentRank);
    }

    [Fact]
    public void RecordScore_ReplacesExistingScore_SameRoundAndDimension()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act
        session.RecordScore(player.Id, 1, "points", 5, _timeProvider);
        session.RecordScore(player.Id, 1, "points", 10, _timeProvider);

        // Assert
        Assert.Single(session.RoundScores);
        Assert.Equal(10, session.RoundScores[0].Value);
        Assert.Equal(10, player.TotalScore);
    }

    [Fact]
    public void RecordScore_MultipleRounds_SumsTotalScore()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act
        session.RecordScore(player.Id, 1, "points", 5, _timeProvider);
        session.RecordScore(player.Id, 2, "points", 10, _timeProvider);

        // Assert
        Assert.Equal(2, session.RoundScores.Count);
        Assert.Equal(15, player.TotalScore);
    }

    [Fact]
    public void RecordScore_InvalidDimension_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            session.RecordScore(player.Id, 1, "nonexistent", 10, _timeProvider));
    }

    [Fact]
    public void RecordScore_NonExistentPlayer_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            session.RecordScore(Guid.NewGuid(), 1, "points", 10, _timeProvider));
    }

    [Fact]
    public void RecordScore_SessionNotStarted_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            session.RecordScore(player.Id, 1, "points", 10, _timeProvider));
    }

    [Fact]
    public void RecordScore_RaisesScoreRecordedEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.RecordScore(player.Id, 1, "points", 10, _timeProvider);

        // Assert
        Assert.Single(session.DomainEvents);
        var evt = Assert.IsType<LiveSessionScoreRecordedEvent>(session.DomainEvents.First());
        Assert.Equal(player.Id, evt.PlayerId);
        Assert.Equal(1, evt.Round);
        Assert.Equal("points", evt.Dimension);
        Assert.Equal(10, evt.Value);
    }

    #endregion

    #region Turn Management

    [Fact]
    public void AdvanceTurn_InProgress_IncrementsIndex()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act
        session.AdvanceTurn(_timeProvider);

        // Assert
        Assert.Equal(2, session.CurrentTurnIndex);
    }

    [Fact]
    public void AdvanceTurn_RaisesTurnAdvancedEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.AdvanceTurn(_timeProvider);

        // Assert
        Assert.Single(session.DomainEvents);
        Assert.IsType<LiveSessionTurnAdvancedEvent>(session.DomainEvents.First());
    }

    [Fact]
    public void AdvanceTurn_NotInProgress_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);

        // Act & Assert
        Assert.Throws<ConflictException>(() => session.AdvanceTurn(_timeProvider));
    }

    [Fact]
    public void AdvanceTurn_CreatesTurnRecords()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act
        session.AdvanceTurn(_timeProvider);

        // Assert
        Assert.True(session.TurnRecords.Count >= 1);
    }

    [Fact]
    public void GetCurrentTurnPlayerId_CyclesThroughPlayers()
    {
        // Arrange
        var session = CreateDefaultSession();
        var p1 = AddDefaultPlayer(session, "P1", PlayerColor.Red);
        var p2 = AddDefaultPlayer(session, "P2", PlayerColor.Blue);
        session.Start(_timeProvider);

        // Turn 1: P1
        var turn1Player = session.GetCurrentTurnPlayerId();
        Assert.Equal(p1.Id, turn1Player);

        // Turn 2: P2
        session.AdvanceTurn(_timeProvider);
        var turn2Player = session.GetCurrentTurnPlayerId();
        Assert.Equal(p2.Id, turn2Player);

        // Turn 3: P1 again
        session.AdvanceTurn(_timeProvider);
        var turn3Player = session.GetCurrentTurnPlayerId();
        Assert.Equal(p1.Id, turn3Player);
    }

    #endregion

    #region Notes and State

    [Fact]
    public void UpdateNotes_ValidNotes_Updates()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        session.UpdateNotes("Round 1 notes", _timeProvider);

        // Assert
        Assert.Equal("Round 1 notes", session.Notes);
    }

    [Fact]
    public void UpdateNotes_Null_ClearsNotes()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.UpdateNotes("Some notes", _timeProvider);

        // Act
        session.UpdateNotes(null, _timeProvider);

        // Assert
        Assert.Null(session.Notes);
    }

    [Fact]
    public void UpdateNotes_TooLong_ThrowsValidationException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var longNotes = new string('x', 2001);

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            session.UpdateNotes(longNotes, _timeProvider));
    }

    [Fact]
    public void LinkToolkit_ValidId_SetsToolkitId()
    {
        // Arrange
        var session = CreateDefaultSession();
        var toolkitId = Guid.NewGuid();

        // Act
        session.LinkToolkit(toolkitId, _timeProvider);

        // Assert
        Assert.Equal(toolkitId, session.ToolkitId);
    }

    [Fact]
    public void LinkToolkit_EmptyId_ThrowsValidationException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            session.LinkToolkit(Guid.Empty, _timeProvider));
    }

    [Fact]
    public void SetAgentMode_Assistant_WithChatSession_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();
        var chatSessionId = Guid.NewGuid();

        // Act
        session.SetAgentMode(AgentSessionMode.Assistant, chatSessionId, _timeProvider);

        // Assert
        Assert.Equal(AgentSessionMode.Assistant, session.AgentMode);
        Assert.Equal(chatSessionId, session.ChatSessionId);
    }

    [Fact]
    public void SetAgentMode_None_ClearsChatSession()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.SetAgentMode(AgentSessionMode.Assistant, Guid.NewGuid(), _timeProvider);

        // Act
        session.SetAgentMode(AgentSessionMode.None, null, _timeProvider);

        // Assert
        Assert.Equal(AgentSessionMode.None, session.AgentMode);
        Assert.Null(session.ChatSessionId);
    }

    [Fact]
    public void SetAgentMode_WithoutChatSession_ThrowsValidationException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            session.SetAgentMode(AgentSessionMode.GameMaster, null, _timeProvider));
    }

    #endregion

    #region Save Method

    [Fact]
    public void Save_FromSetup_SetsLastSavedAtAndRaisesEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.MoveToSetup(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Save(_timeProvider);

        // Assert
        Assert.Equal(_now, session.LastSavedAt);
        Assert.Equal(_now, session.UpdatedAt);
        Assert.Single(session.DomainEvents);
        var evt = Assert.IsType<LiveSessionSavedEvent>(session.DomainEvents.First());
        Assert.Equal(session.Id, evt.SessionId);
        Assert.Equal(_now, evt.SavedAt);
    }

    [Fact]
    public void Save_FromInProgress_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Save(_timeProvider);

        // Assert
        Assert.Equal(_now, session.LastSavedAt);
        Assert.Single(session.DomainEvents);
        Assert.IsType<LiveSessionSavedEvent>(session.DomainEvents.First());
    }

    [Fact]
    public void Save_FromPaused_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Pause(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Save(_timeProvider);

        // Assert
        Assert.Equal(_now, session.LastSavedAt);
        Assert.Single(session.DomainEvents);
        Assert.IsType<LiveSessionSavedEvent>(session.DomainEvents.First());
    }

    [Fact]
    public void Save_FromCreated_ThrowsConflictException()
    {
        // Arrange (Created is not IsActive)
        var session = CreateDefaultSession();

        // Act & Assert
        Assert.Throws<ConflictException>(() => session.Save(_timeProvider));
    }

    [Fact]
    public void Save_FromCompleted_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Complete(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() => session.Save(_timeProvider));
    }

    [Fact]
    public void Save_MultipleTimes_UpdatesLastSavedAt()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.MoveToSetup(_timeProvider);
        session.Save(_timeProvider);
        var firstSaveTime = session.LastSavedAt;

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        session.ClearDomainEvents();

        // Act
        session.Save(_timeProvider);

        // Assert
        Assert.NotEqual(firstSaveTime, session.LastSavedAt);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, session.LastSavedAt);
    }

    #endregion

    #region Completed Session Immutability

    [Fact]
    public void RemovePlayer_CompletedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session, "Solo", PlayerColor.Red);
        session.Start(_timeProvider);
        session.Complete(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            session.RemovePlayer(player.Id, _timeProvider));
    }

    [Fact]
    public void SetTurnOrder_CompletedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Complete(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            session.SetTurnOrder(new List<Guid> { player.Id }, _timeProvider));
    }

    [Fact]
    public void CreateTeam_CompletedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Complete(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            session.CreateTeam("Alpha", "#FF0000", _timeProvider));
    }

    [Fact]
    public void AssignPlayerToTeam_CompletedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        var team = session.CreateTeam("Alpha", "#FF0000", _timeProvider);
        session.Start(_timeProvider);
        session.Complete(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            session.AssignPlayerToTeam(player.Id, team.Id, _timeProvider));
    }

    [Fact]
    public void UpdateNotes_CompletedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Complete(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            session.UpdateNotes("New notes", _timeProvider));
    }

    [Fact]
    public void UpdateGameState_CompletedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Complete(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            session.UpdateGameState(null, _timeProvider));
    }

    [Fact]
    public void LinkToolkit_CompletedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Complete(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            session.LinkToolkit(Guid.NewGuid(), _timeProvider));
    }

    [Fact]
    public void SetAgentMode_CompletedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.Complete(_timeProvider);

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            session.SetAgentMode(AgentSessionMode.Assistant, Guid.NewGuid(), _timeProvider));
    }

    #endregion

    #region Domain Event Emissions

    [Fact]
    public void CreateTeam_RaisesTeamCreatedEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.ClearDomainEvents();

        // Act
        var team = session.CreateTeam("Alpha", "#FF0000", _timeProvider);

        // Assert
        Assert.Single(session.DomainEvents);
        var evt = Assert.IsType<LiveSessionTeamCreatedEvent>(session.DomainEvents.First());
        Assert.Equal(session.Id, evt.SessionId);
        Assert.Equal(team.Id, evt.TeamId);
        Assert.Equal("Alpha", evt.TeamName);
    }

    [Fact]
    public void AssignPlayerToTeam_RaisesPlayerAssignedToTeamEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        var team = session.CreateTeam("Alpha", "#FF0000", _timeProvider);
        session.ClearDomainEvents();

        // Act
        session.AssignPlayerToTeam(player.Id, team.Id, _timeProvider);

        // Assert
        Assert.Single(session.DomainEvents);
        var evt = Assert.IsType<LiveSessionPlayerAssignedToTeamEvent>(session.DomainEvents.First());
        Assert.Equal(session.Id, evt.SessionId);
        Assert.Equal(player.Id, evt.PlayerId);
        Assert.Equal(team.Id, evt.TeamId);
    }

    [Fact]
    public void SetTurnOrder_RaisesTurnOrderChangedEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        var p1 = AddDefaultPlayer(session, "P1", PlayerColor.Red);
        var p2 = AddDefaultPlayer(session, "P2", PlayerColor.Blue);
        session.ClearDomainEvents();

        // Act
        session.SetTurnOrder(new List<Guid> { p2.Id, p1.Id }, _timeProvider);

        // Assert
        Assert.Single(session.DomainEvents);
        var evt = Assert.IsType<LiveSessionTurnOrderChangedEvent>(session.DomainEvents.First());
        Assert.Equal(session.Id, evt.SessionId);
        Assert.Equal(2, evt.NewTurnOrder.Count);
        Assert.Equal(p2.Id, evt.NewTurnOrder[0]);
        Assert.Equal(p1.Id, evt.NewTurnOrder[1]);
    }

    #endregion

    #region Completed Event Snapshot Data

    [Fact]
    public void Complete_IncludesPlayerSnapshotsInEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        var userId = Guid.NewGuid();
        var p1 = AddDefaultPlayer(session, "Alice", PlayerColor.Red, userId);
        var p2 = AddDefaultPlayer(session, "Bob", PlayerColor.Blue);
        session.Start(_timeProvider);
        session.RecordScore(p1.Id, 1, "points", 10, _timeProvider);
        session.RecordScore(p2.Id, 1, "points", 5, _timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Complete(_timeProvider);

        // Assert
        var evt = Assert.IsType<LiveSessionCompletedEvent>(session.DomainEvents.First());
        Assert.Equal(2, evt.Players.Count);

        var aliceSnapshot = evt.Players.First(p => p.DisplayName == "Alice");
        Assert.Equal(p1.Id, aliceSnapshot.PlayerId);
        Assert.Equal(userId, aliceSnapshot.UserId);
        Assert.Equal(10, aliceSnapshot.TotalScore);
        Assert.Equal(1, aliceSnapshot.CurrentRank);

        var bobSnapshot = evt.Players.First(p => p.DisplayName == "Bob");
        Assert.Equal(5, bobSnapshot.TotalScore);
        Assert.Equal(2, bobSnapshot.CurrentRank);
    }

    [Fact]
    public void Complete_ExcludesSpectatorsFromPlayerSnapshots()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session, "Player", PlayerColor.Red);
        AddDefaultPlayer(session, "Spectator", PlayerColor.Blue, role: PlayerRole.Spectator);
        session.Start(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Complete(_timeProvider);

        // Assert
        var evt = Assert.IsType<LiveSessionCompletedEvent>(session.DomainEvents.First());
        Assert.Single(evt.Players);
        Assert.Equal("Player", evt.Players[0].DisplayName);
    }

    [Fact]
    public void Complete_IncludesScoreSnapshotsInEvent()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.RecordScore(player.Id, 1, "points", 10, _timeProvider, "pts");
        session.RecordScore(player.Id, 2, "points", 20, _timeProvider, "pts");
        session.ClearDomainEvents();

        // Act
        session.Complete(_timeProvider);

        // Assert
        var evt = Assert.IsType<LiveSessionCompletedEvent>(session.DomainEvents.First());
        Assert.Equal(2, evt.Scores.Count);
        Assert.All(evt.Scores, s =>
        {
            Assert.Equal(player.Id, s.PlayerId);
            Assert.Equal("points", s.Dimension);
            Assert.Equal("pts", s.Unit);
        });
        Assert.Equal(10, evt.Scores[0].Value);
        Assert.Equal(20, evt.Scores[1].Value);
    }

    [Fact]
    public void Complete_IncludesSessionMetadataInEvent()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var session = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", _timeProvider,
            gameId: gameId, visibility: PlayRecordVisibility.Group, groupId: groupId);
        AddDefaultPlayer(session);
        session.UpdateNotes("Game notes", _timeProvider);
        session.Start(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Complete(_timeProvider);

        // Assert
        var evt = Assert.IsType<LiveSessionCompletedEvent>(session.DomainEvents.First());
        Assert.Equal(gameId, evt.GameId);
        Assert.Equal("Catan", evt.GameName);
        Assert.Equal(session.CreatedByUserId, evt.CreatedByUserId);
        Assert.Equal(PlayRecordVisibility.Group, evt.Visibility);
        Assert.Equal(groupId, evt.GroupId);
        Assert.Equal("Game notes", evt.Notes);
        Assert.NotNull(evt.StartedAt);
    }

    [Fact]
    public void Complete_WithNoScores_IncludesEmptyScoreSnapshots()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Complete(_timeProvider);

        // Assert
        var evt = Assert.IsType<LiveSessionCompletedEvent>(session.DomainEvents.First());
        Assert.Empty(evt.Scores);
        Assert.Single(evt.Players);
    }

    #endregion

    #region Query Methods

    [Fact]
    public void GetPlayer_ExistingId_ReturnsPlayer()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);

        // Act
        var found = session.GetPlayer(player.Id);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(player.Id, found.Id);
    }

    [Fact]
    public void GetPlayer_NonExistentId_ReturnsNull()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        var found = session.GetPlayer(Guid.NewGuid());

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public void Host_ReturnsHostPlayer()
    {
        // Arrange
        var session = CreateDefaultSession();
        var host = AddDefaultPlayer(session, "Host", PlayerColor.Red);
        AddDefaultPlayer(session, "Player 2", PlayerColor.Blue);

        // Assert
        Assert.NotNull(session.Host);
        Assert.Equal(host.Id, session.Host.Id);
    }

    [Fact]
    public void Host_NoPlayers_ReturnsNull()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Assert
        Assert.Null(session.Host);
    }

    #endregion
}
