using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using FluentAssertions;

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
        session.Id.Should().Be(id);
        session.CreatedByUserId.Should().Be(userId);
        session.GameName.Should().Be("Catan");
        session.GameId.Should().Be(gameId);
        session.Status.Should().Be(LiveSessionStatus.Created);
        session.SessionCode.Should().NotBeNull();
        session.SessionCode.Length.Should().Be(6);
        session.CreatedAt.Should().Be(_now);
        session.UpdatedAt.Should().Be(_now);
        session.CurrentTurnIndex.Should().Be(0);
        session.ScoringConfig.Should().NotBeNull();
        session.AgentMode.Should().Be(AgentSessionMode.None);
        session.StartedAt.Should().BeNull();
        session.PausedAt.Should().BeNull();
        session.CompletedAt.Should().BeNull();
        session.Players.Should().BeEmpty();
        session.Teams.Should().BeEmpty();
    }

    [Fact]
    public void Create_RaisesLiveSessionCreatedEvent()
    {
        // Act
        var session = CreateDefaultSession();

        // Assert
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionCreatedEvent>();
        var evt = (LiveSessionCreatedEvent)session.DomainEvents.First();
        evt.SessionId.Should().Be(session.Id);
        evt.CreatedByUserId.Should().Be(session.CreatedByUserId);
        evt.GameName.Should().Be("Catan");
    }

    [Fact]
    public void Create_WithGroupVisibility_RequiresGroupId()
    {
        // Act & Assert
        var act = () =>
            LiveGameSession.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Catan",
                _timeProvider,
                visibility: PlayRecordVisibility.Group,
                groupId: null);
        act.Should().Throw<ValidationException>();
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
        session.Visibility.Should().Be(PlayRecordVisibility.Group);
        session.GroupId.Should().Be(groupId);
    }

    [Fact]
    public void Create_EmptyId_ThrowsValidationException()
    {
        var act = () =>
            LiveGameSession.Create(Guid.Empty, Guid.NewGuid(), "Catan", _timeProvider);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_EmptyUserId_ThrowsValidationException()
    {
        var act = () =>
            LiveGameSession.Create(Guid.NewGuid(), Guid.Empty, "Catan", _timeProvider);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_EmptyGameName_ThrowsValidationException()
    {
        var act = () =>
            LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), "", _timeProvider);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_GameNameTooLong_ThrowsValidationException()
    {
        var longName = new string('a', 256);
        var act = () =>
            LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), longName, _timeProvider);
        act.Should().Throw<ValidationException>();
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
        session.ScoringConfig.EnabledDimensions.Count.Should().Be(2);
    }

    [Fact]
    public void Create_WithAgentMode_SetsMode()
    {
        // Act
        var session = LiveGameSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", _timeProvider,
            agentMode: AgentSessionMode.Assistant);

        // Assert
        session.AgentMode.Should().Be(AgentSessionMode.Assistant);
    }

    [Fact]
    public void Create_SessionCode_IsAlphanumericAndReadable()
    {
        // Create multiple sessions to check code generation
        for (var i = 0; i < 10; i++)
        {
            var session = CreateDefaultSession();
            session.SessionCode.Should().MatchRegex("^[A-Z2-9]{6}$");
            // Should not contain I, O, 0, 1 (confusing characters)
            session.SessionCode.Should().NotContain("I");
            session.SessionCode.Should().NotContain("O");
            session.SessionCode.Should().NotContain("0");
            session.SessionCode.Should().NotContain("1");
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
        player.Role.Should().Be(PlayerRole.Host);
        session.Players.Should().ContainSingle();
        (session.HasPlayers).Should().BeTrue();
        session.PlayerCount.Should().Be(1);
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
        player.Role.Should().Be(PlayerRole.Player);
        session.Players.Count.Should().Be(2);
    }

    [Fact]
    public void AddPlayer_WithExplicitRole_UsesSpecifiedRole()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        var player = AddDefaultPlayer(session, role: PlayerRole.Spectator);

        // Assert
        player.Role.Should().Be(PlayerRole.Spectator);
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
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionPlayerAddedEvent>();
    }

    [Fact]
    public void AddPlayer_DuplicateUserId_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var userId = Guid.NewGuid();
        AddDefaultPlayer(session, "Player 1", PlayerColor.Red, userId);

        // Act & Assert
        var act = () =>
            AddDefaultPlayer(session, "Player 2", PlayerColor.Blue, userId);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddPlayer_DuplicateDisplayName_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session, "Marco", PlayerColor.Red);

        // Act & Assert
        var act = () =>
            AddDefaultPlayer(session, "Marco", PlayerColor.Blue);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddPlayer_DuplicateColor_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session, "Player 1", PlayerColor.Red);

        // Act & Assert
        var act = () =>
            AddDefaultPlayer(session, "Player 2", PlayerColor.Red);
        act.Should().Throw<DomainException>();
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
        var act = () =>
            AddDefaultPlayer(session, "New Player", PlayerColor.Blue);
        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void AddPlayer_AddsToTurnOrder()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        var player = AddDefaultPlayer(session);

        // Assert
        session.TurnOrder.Should().ContainSingle();
        session.TurnOrder[0].Should().Be(player.Id);
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
        (player.IsActive).Should().BeFalse();
        session.PlayerCount.Should().Be(0);
    }

    [Fact]
    public void RemovePlayer_Host_WithOtherPlayers_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var host = AddDefaultPlayer(session, "Host", PlayerColor.Red);
        AddDefaultPlayer(session, "Player 2", PlayerColor.Blue);

        // Act & Assert
        var act = () =>
            session.RemovePlayer(host.Id, _timeProvider);
        act.Should().Throw<ConflictException>();
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
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionPlayerRemovedEvent>();
    }

    [Fact]
    public void RemovePlayer_NonExistentPlayer_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);

        // Act & Assert
        var act = () =>
            session.RemovePlayer(Guid.NewGuid(), _timeProvider);
        act.Should().Throw<DomainException>();
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
        session.TurnOrder[0].Should().Be(p3.Id);
        session.TurnOrder[1].Should().Be(p1.Id);
        session.TurnOrder[2].Should().Be(p2.Id);
    }

    [Fact]
    public void SetTurnOrder_InvalidPlayerId_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);

        // Act & Assert
        var act = () =>
            session.SetTurnOrder(new List<Guid> { Guid.NewGuid() }, _timeProvider);
        act.Should().Throw<DomainException>();
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
        team.Should().NotBeNull();
        team.Name.Should().Be("Alpha");
        team.Color.Should().Be("#FF0000");
        session.Teams.Should().ContainSingle();
    }

    [Fact]
    public void CreateTeam_DuplicateName_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.CreateTeam("Alpha", "#FF0000", _timeProvider);

        // Act & Assert
        var act = () =>
            session.CreateTeam("Alpha", "#0000FF", _timeProvider);
        act.Should().Throw<DomainException>();
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
        player.TeamId.Should().Be(team.Id);
        team.PlayerIds.Should().ContainSingle();
        team.PlayerIds[0].Should().Be(player.Id);
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
        player.TeamId.Should().Be(team2.Id);
        team1.PlayerIds.Should().BeEmpty();
        team2.PlayerIds.Should().ContainSingle();
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
        session.Status.Should().Be(LiveSessionStatus.Setup);
    }

    [Fact]
    public void MoveToSetup_FromInProgress_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act & Assert
        ((Action)(() => session.MoveToSetup(_timeProvider))).Should().Throw<ConflictException>();
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
        session.Status.Should().Be(LiveSessionStatus.InProgress);
        session.StartedAt.Should().Be(_now);
        session.CurrentTurnIndex.Should().Be(1);
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
        session.Status.Should().Be(LiveSessionStatus.InProgress);
    }

    [Fact]
    public void Start_WithoutPlayers_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        ((Action)(() => session.Start(_timeProvider))).Should().Throw<ConflictException>();
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
        ((Action)(() => session.Start(_timeProvider))).Should().Throw<ConflictException>();
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
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionStartedEvent>();
        var evt = (LiveSessionStartedEvent)session.DomainEvents.First();
        evt.SessionId.Should().Be(session.Id);
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
        session.Status.Should().Be(LiveSessionStatus.Paused);
        session.PausedAt.Should().Be(_now);
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionPausedEvent>();
    }

    [Fact]
    public void Pause_FromSetup_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.MoveToSetup(_timeProvider);

        // Act & Assert
        ((Action)(() => session.Pause(_timeProvider))).Should().Throw<ConflictException>();
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
        session.Status.Should().Be(LiveSessionStatus.InProgress);
        session.PausedAt.Should().BeNull();
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionResumedEvent>();
    }

    [Fact]
    public void Resume_FromInProgress_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act & Assert
        ((Action)(() => session.Resume(_timeProvider))).Should().Throw<ConflictException>();
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
        session.Status.Should().Be(LiveSessionStatus.Completed);
        session.CompletedAt.Should().Be(_now);
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionCompletedEvent>();
        var evt = (LiveSessionCompletedEvent)session.DomainEvents.First();
        evt.TotalTurns.Should().Be(1);
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
        session.Status.Should().Be(LiveSessionStatus.Completed);
    }

    [Fact]
    public void Complete_FromCreated_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        ((Action)(() => session.Complete(_timeProvider))).Should().Throw<ConflictException>();
    }

    [Fact]
    public void IsActive_ReturnsCorrectly()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Created is not "active" (no setup yet)
        (session.IsActive).Should().BeFalse();

        session.MoveToSetup(_timeProvider);
        (session.IsActive).Should().BeTrue();

        AddDefaultPlayer(session);
        session.Start(_timeProvider);
        (session.IsActive).Should().BeTrue();

        session.Pause(_timeProvider);
        (session.IsActive).Should().BeTrue();

        session.Complete(_timeProvider);
        (session.IsActive).Should().BeFalse();
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
        session.RoundScores.Should().ContainSingle();
        session.RoundScores[0].Value.Should().Be(10);
        player.TotalScore.Should().Be(10);
        player.CurrentRank.Should().Be(1);
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
        p1.CurrentRank.Should().Be(2);
        p2.CurrentRank.Should().Be(1);
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
        p1.CurrentRank.Should().Be(1);
        p2.CurrentRank.Should().Be(1);
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
        session.RoundScores.Should().ContainSingle();
        session.RoundScores[0].Value.Should().Be(10);
        player.TotalScore.Should().Be(10);
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
        session.RoundScores.Count.Should().Be(2);
        player.TotalScore.Should().Be(15);
    }

    [Fact]
    public void RecordScore_InvalidDimension_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act & Assert
        var act = () =>
            session.RecordScore(player.Id, 1, "nonexistent", 10, _timeProvider);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordScore_NonExistentPlayer_ThrowsDomainException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);
        session.Start(_timeProvider);

        // Act & Assert
        var act = () =>
            session.RecordScore(Guid.NewGuid(), 1, "points", 10, _timeProvider);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordScore_SessionNotStarted_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var player = AddDefaultPlayer(session);

        // Act & Assert
        var act = () =>
            session.RecordScore(player.Id, 1, "points", 10, _timeProvider);
        act.Should().Throw<ConflictException>();
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
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionScoreRecordedEvent>();
        var evt = (LiveSessionScoreRecordedEvent)session.DomainEvents.First();
        evt.PlayerId.Should().Be(player.Id);
        evt.Round.Should().Be(1);
        evt.Dimension.Should().Be("points");
        evt.Value.Should().Be(10);
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
        session.CurrentTurnIndex.Should().Be(2);
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
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionTurnAdvancedEvent>();
    }

    [Fact]
    public void AdvanceTurn_NotInProgress_ThrowsConflictException()
    {
        // Arrange
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);

        // Act & Assert
        ((Action)(() => session.AdvanceTurn(_timeProvider))).Should().Throw<ConflictException>();
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
        (session.TurnRecords.Count >= 1).Should().BeTrue();
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
        turn1Player.Should().Be(p1.Id);

        // Turn 2: P2
        session.AdvanceTurn(_timeProvider);
        var turn2Player = session.GetCurrentTurnPlayerId();
        turn2Player.Should().Be(p2.Id);

        // Turn 3: P1 again
        session.AdvanceTurn(_timeProvider);
        var turn3Player = session.GetCurrentTurnPlayerId();
        turn3Player.Should().Be(p1.Id);
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
        session.Notes.Should().Be("Round 1 notes");
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
        session.Notes.Should().BeNull();
    }

    [Fact]
    public void UpdateNotes_TooLong_ThrowsValidationException()
    {
        // Arrange
        var session = CreateDefaultSession();
        var longNotes = new string('x', 2001);

        // Act & Assert
        var act = () =>
            session.UpdateNotes(longNotes, _timeProvider);
        act.Should().Throw<ValidationException>();
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
        session.ToolkitId.Should().Be(toolkitId);
    }

    [Fact]
    public void LinkToolkit_EmptyId_ThrowsValidationException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        var act = () =>
            session.LinkToolkit(Guid.Empty, _timeProvider);
        act.Should().Throw<ValidationException>();
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
        session.AgentMode.Should().Be(AgentSessionMode.Assistant);
        session.ChatSessionId.Should().Be(chatSessionId);
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
        session.AgentMode.Should().Be(AgentSessionMode.None);
        session.ChatSessionId.Should().BeNull();
    }

    [Fact]
    public void SetAgentMode_WithoutChatSession_ThrowsValidationException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        var act = () =>
            session.SetAgentMode(AgentSessionMode.GameMaster, null, _timeProvider);
        act.Should().Throw<ValidationException>();
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
        session.LastSavedAt.Should().Be(_now);
        session.UpdatedAt.Should().Be(_now);
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionSavedEvent>();
        var evt = (LiveSessionSavedEvent)session.DomainEvents.First();
        evt.SessionId.Should().Be(session.Id);
        evt.SavedAt.Should().Be(_now);
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
        session.LastSavedAt.Should().Be(_now);
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionSavedEvent>();
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
        session.LastSavedAt.Should().Be(_now);
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionSavedEvent>();
    }

    [Fact]
    public void Save_FromCreated_ThrowsConflictException()
    {
        // Arrange (Created is not IsActive)
        var session = CreateDefaultSession();

        // Act & Assert
        ((Action)(() => session.Save(_timeProvider))).Should().Throw<ConflictException>();
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
        ((Action)(() => session.Save(_timeProvider))).Should().Throw<ConflictException>();
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
        session.LastSavedAt.Should().NotBe(firstSaveTime);
        session.LastSavedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
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
        var act = () =>
            session.RemovePlayer(player.Id, _timeProvider);
        act.Should().Throw<ConflictException>();
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
        var act = () =>
            session.SetTurnOrder(new List<Guid> { player.Id }, _timeProvider);
        act.Should().Throw<ConflictException>();
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
        var act = () =>
            session.CreateTeam("Alpha", "#FF0000", _timeProvider);
        act.Should().Throw<ConflictException>();
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
        var act = () =>
            session.AssignPlayerToTeam(player.Id, team.Id, _timeProvider);
        act.Should().Throw<ConflictException>();
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
        var act = () =>
            session.UpdateNotes("New notes", _timeProvider);
        act.Should().Throw<ConflictException>();
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
        var act = () =>
            session.UpdateGameState(null, _timeProvider);
        act.Should().Throw<ConflictException>();
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
        var act = () =>
            session.LinkToolkit(Guid.NewGuid(), _timeProvider);
        act.Should().Throw<ConflictException>();
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
        var act = () =>
            session.SetAgentMode(AgentSessionMode.Assistant, Guid.NewGuid(), _timeProvider);
        act.Should().Throw<ConflictException>();
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
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionTeamCreatedEvent>();
        var evt = (LiveSessionTeamCreatedEvent)session.DomainEvents.First();
        evt.SessionId.Should().Be(session.Id);
        evt.TeamId.Should().Be(team.Id);
        evt.TeamName.Should().Be("Alpha");
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
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionPlayerAssignedToTeamEvent>();
        var evt = (LiveSessionPlayerAssignedToTeamEvent)session.DomainEvents.First();
        evt.SessionId.Should().Be(session.Id);
        evt.PlayerId.Should().Be(player.Id);
        evt.TeamId.Should().Be(team.Id);
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
        session.DomainEvents.Should().ContainSingle();
        session.DomainEvents.First().Should().BeOfType<LiveSessionTurnOrderChangedEvent>();
        var evt = (LiveSessionTurnOrderChangedEvent)session.DomainEvents.First();
        evt.SessionId.Should().Be(session.Id);
        evt.NewTurnOrder.Count.Should().Be(2);
        evt.NewTurnOrder[0].Should().Be(p2.Id);
        evt.NewTurnOrder[1].Should().Be(p1.Id);
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
        session.DomainEvents.First().Should().BeOfType<LiveSessionCompletedEvent>();
        var evt = (LiveSessionCompletedEvent)session.DomainEvents.First();
        evt.Players.Count.Should().Be(2);

        var aliceSnapshot = evt.Players.First(p => p.DisplayName == "Alice");
        aliceSnapshot.PlayerId.Should().Be(p1.Id);
        aliceSnapshot.UserId.Should().Be(userId);
        aliceSnapshot.TotalScore.Should().Be(10);
        aliceSnapshot.CurrentRank.Should().Be(1);

        var bobSnapshot = evt.Players.First(p => p.DisplayName == "Bob");
        bobSnapshot.TotalScore.Should().Be(5);
        bobSnapshot.CurrentRank.Should().Be(2);
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
        session.DomainEvents.First().Should().BeOfType<LiveSessionCompletedEvent>();
        var evt = (LiveSessionCompletedEvent)session.DomainEvents.First();
        evt.Players.Should().ContainSingle();
        evt.Players[0].DisplayName.Should().Be("Player");
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
        session.DomainEvents.First().Should().BeOfType<LiveSessionCompletedEvent>();
        var evt = (LiveSessionCompletedEvent)session.DomainEvents.First();
        evt.Scores.Count.Should().Be(2);
        Assert.All(evt.Scores, s =>
        {
            s.PlayerId.Should().Be(player.Id);
            s.Dimension.Should().Be("points");
            s.Unit.Should().Be("pts");
        });
        evt.Scores[0].Value.Should().Be(10);
        evt.Scores[1].Value.Should().Be(20);
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
        session.DomainEvents.First().Should().BeOfType<LiveSessionCompletedEvent>();
        var evt = (LiveSessionCompletedEvent)session.DomainEvents.First();
        evt.GameId.Should().Be(gameId);
        evt.GameName.Should().Be("Catan");
        evt.CreatedByUserId.Should().Be(session.CreatedByUserId);
        evt.Visibility.Should().Be(PlayRecordVisibility.Group);
        evt.GroupId.Should().Be(groupId);
        evt.Notes.Should().Be("Game notes");
        evt.StartedAt.Should().NotBeNull();
        // SessionDate should reflect when game was played (StartedAt), not entity creation
        evt.SessionDate.Should().Be(session.StartedAt);
    }

    [Fact]
    public void Complete_SessionDate_UsesStartedAtNotCreatedAt()
    {
        // Arrange - advance time between creation and start to differentiate them
        var session = CreateDefaultSession();
        AddDefaultPlayer(session);

        _timeProvider.Advance(TimeSpan.FromDays(2)); // Create on day 0, start on day 2
        session.Start(_timeProvider);
        session.ClearDomainEvents();

        // Act
        session.Complete(_timeProvider);

        // Assert
        session.DomainEvents.First().Should().BeOfType<LiveSessionCompletedEvent>();
        var evt = (LiveSessionCompletedEvent)session.DomainEvents.First();
        evt.SessionDate.Should().NotBe(session.CreatedAt);
        evt.SessionDate.Should().Be(session.StartedAt);
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
        session.DomainEvents.First().Should().BeOfType<LiveSessionCompletedEvent>();
        var evt = (LiveSessionCompletedEvent)session.DomainEvents.First();
        evt.Scores.Should().BeEmpty();
        evt.Players.Should().ContainSingle();
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
        found.Should().NotBeNull();
        found.Id.Should().Be(player.Id);
    }

    [Fact]
    public void GetPlayer_NonExistentId_ReturnsNull()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        var found = session.GetPlayer(Guid.NewGuid());

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public void Host_ReturnsHostPlayer()
    {
        // Arrange
        var session = CreateDefaultSession();
        var host = AddDefaultPlayer(session, "Host", PlayerColor.Red);
        AddDefaultPlayer(session, "Player 2", PlayerColor.Blue);

        // Assert
        session.Host.Should().NotBeNull();
        session.Host.Id.Should().Be(host.Id);
    }

    [Fact]
    public void Host_NoPlayers_ReturnsNull()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Assert
        session.Host.Should().BeNull();
    }

    #endregion
}
