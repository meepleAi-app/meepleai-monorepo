using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// Spec 2026-10-11 §3.2 e §5.1: la porta di sola lettura degli strumenti, sopra le query della sessione
/// live. Qui la traduzione dal DTO di GameManagement allo stato che vede l'assistente.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class LiveSessionAssistantReaderTests
{
    private static readonly Guid SessionId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Game = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Giulia = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Luca = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Spettatrice = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid Uscito = Guid.Parse("00000000-0000-0000-0000-000000000005");
    private static readonly DateTime Now = new(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMediator> _mediator = new();

    private static LiveSessionPlayerDto Player(Guid id, string name, int total, PlayerRole role = PlayerRole.Player, bool active = true) =>
        new(id, UserId: null, name, AvatarUrl: null, PlayerColor.Red, role, TeamId: null, total, CurrentRank: 1, Now, active);

    private static LiveSessionDto Session(
        LiveSessionStatus status = LiveSessionStatus.InProgress, int turnIndex = 4, Guid? currentPlayer = null, Guid? gameId = null) =>
        new(
            Id: SessionId,
            SessionCode: "ABC123",
            GameId: gameId,
            GameName: "Azul",
            GameSlug: "azul",
            CreatedByUserId: Owner,
            Status: status,
            Visibility: PlayRecordVisibility.Private,
            GroupId: null,
            CreatedAt: Now,
            StartedAt: Now,
            PausedAt: null,
            CompletedAt: null,
            UpdatedAt: Now,
            LastSavedAt: null,
            CurrentTurnIndex: turnIndex,
            CurrentTurnPlayerId: currentPlayer ?? Marco,
            AgentMode: AgentSessionMode.None,
            Notes: null,
            GameState: null,
            Players:
            [
                Player(Marco, "Marco", 7, PlayerRole.Host),
                Player(Giulia, "Giulia", 12),
                Player(Luca, "Luca", 0),
                Player(Spettatrice, "Anna", 0, PlayerRole.Spectator),
                Player(Uscito, "Paolo", 3, active: false),
            ],
            Teams: [],
            RoundScores: [new LiveSessionRoundScoreDto(Marco, 1, "punti", 7, Unit: null, Now)],
            ScoringConfig: new LiveSessionScoringConfigDto(["punti"], new Dictionary<string, string>(StringComparer.Ordinal)));

    private LiveSessionAssistantReader Reader(LiveSessionDto session, string? phase = null)
    {
        _mediator.Setup(m => m.Send(It.Is<GetLiveSessionQuery>(q => q.SessionId == SessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mediator.Setup(m => m.Send(It.Is<GetTurnPhasesQuery>(q => q.SessionId == SessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TurnPhasesDto(
                session.CurrentTurnIndex, 0, phase, phase is null ? Array.Empty<string>() : new[] { phase }, phase is null ? 0 : 1, phase is not null));
        return new LiveSessionAssistantReader(_mediator.Object, SessionId, Owner);
    }

    [Fact]
    public async Task State_HasTheActivePlayers_AndTheRoundDerivedFromThem()
    {
        // Indice 4 con tre giocatori nel giro: è il primo turno del secondo giro. La spettatrice e chi è
        // uscito non contano, altrimenti il round resterebbe 1. (L'ordine di turno impostato a mano non
        // arriva nel DTO: è il limite dichiarato nello scostamento S1.)
        var state = await Reader(Session(turnIndex: 4), phase: "Rifornimento").GetStateAsync(TestContext.Current.CancellationToken);

        state.Players.Should().Equal(
            new AssistantPlayer(Marco, "Marco", 7, true),
            new AssistantPlayer(Giulia, "Giulia", 12, true),
            new AssistantPlayer(Luca, "Luca", 0, true));
        state.CurrentTurnIndex.Should().Be(4);
        state.CurrentRound.Should().Be(2);
        state.CurrentTurnPlayerId.Should().Be(Marco);
        state.CurrentPhase.Should().Be("Rifornimento");
        state.Dimensions.Should().Equal("punti");
        state.Scores.Should().Equal(new AssistantScoreCell(Marco, 1, "punti", 7));
        state.Status.Should().Be("InProgress");
        state.CanRecordScores.Should().BeTrue();
        state.CanAdvanceTurn.Should().BeTrue();
        state.GameName.Should().Be("Azul");
    }

    /// <summary>Review Focus 6: il primo turno di una partita non ancora avviata.</summary>
    [Fact]
    public async Task Round_IsOneBeforeTheSessionStarts_AndNothingCanBeWritten()
    {
        // Com'è una sessione appena creata con i suoi giocatori: indice 0, e il dominio indica già il
        // primo in ordine di turno.
        var state = await Reader(Session(LiveSessionStatus.Created, turnIndex: 0))
            .GetStateAsync(TestContext.Current.CancellationToken);

        state.CurrentRound.Should().Be(1);
        state.CurrentTurnPlayerId.Should().Be(Marco);
        state.Status.Should().Be("Created");
        state.CanRecordScores.Should().BeFalse();
        state.CanAdvanceTurn.Should().BeFalse();
    }

    [Theory]
    [InlineData(LiveSessionStatus.Paused, true, false)]
    [InlineData(LiveSessionStatus.Completed, false, false)]
    public async Task WhatCanBeWritten_FollowsTheSessionStatus(LiveSessionStatus status, bool scores, bool turn)
    {
        var state = await Reader(Session(status)).GetStateAsync(TestContext.Current.CancellationToken);

        state.CanRecordScores.Should().Be(scores);
        state.CanAdvanceTurn.Should().Be(turn);
    }

    [Fact]
    public async Task HouseRules_AreTheOnesOfTheGame_ForTheOwner()
    {
        _mediator.Setup(m => m.Send(It.Is<GetGameMemoryQuery>(q => q.GameId == Game && q.OwnerId == Owner), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameMemoryDto(Guid.NewGuid(), Game, Owner,
                [new HouseRuleDto(Guid.NewGuid(), "Si gioca fino a 5 round", Now, "manual")], []));

        var state = await Reader(Session(gameId: Game)).GetStateAsync(TestContext.Current.CancellationToken);

        state.GameId.Should().Be(Game);
        state.HouseRules.Should().Equal("Si gioca fino a 5 round");
    }

    [Fact]
    public async Task WithoutAGame_ThereAreNoHouseRules_AndTheMemoryIsNotAsked()
    {
        var state = await Reader(Session(gameId: null)).GetStateAsync(TestContext.Current.CancellationToken);

        state.GameId.Should().BeNull();
        state.HouseRules.Should().BeEmpty();
        _mediator.Verify(m => m.Send(It.IsAny<GetGameMemoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(LiveSessionStatus.Created, true)]
    [InlineData(LiveSessionStatus.Setup, true)]
    [InlineData(LiveSessionStatus.InProgress, true)]
    [InlineData(LiveSessionStatus.Paused, true)]
    [InlineData(LiveSessionStatus.Completed, false)]
    public async Task Access_SaysWhoOwnsTheSession_AndWhetherTheAssistantAnswers(LiveSessionStatus status, bool accepts)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetLiveSessionQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(Session(status));

        var access = await new LiveSessionAssistantDirectory(_mediator.Object)
            .GetAccessAsync(SessionId, TestContext.Current.CancellationToken);

        access.Should().Be(new AssistantSessionAccess(Owner, status.ToString(), accepts));
    }
}
