using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>Spec §5.1: gli strumenti leggono dalle porte e propongono soltanto.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4240")]
public sealed class SessionAssistantToolsetTests
{
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Giulia = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Luca = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private readonly Mock<ISessionAssistantReader> _reader = new();
    private readonly List<ScoreProposalDraft> _scores = [];
    private readonly List<TurnProposalDraft> _turns = [];
    private readonly Mock<IAssistantPlayerMatcher> _matcher = new();

    public SessionAssistantToolsetTests()
    {
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State());
        _matcher.Setup(m => m.Match("Marco", It.IsAny<IReadOnlyList<AssistantPlayer>>()))
            .Returns(new AssistantPlayerMatch(Marco, "Marco", false, []));
    }

    private SessionAssistantToolset Toolset()
    {
        var sink = new Mock<IAssistantProposalSink>();
        sink.Setup(s => s.ProposeScoreAsync(It.IsAny<ScoreProposalDraft>(), It.IsAny<CancellationToken>()))
            .Callback<ScoreProposalDraft, CancellationToken>((d, _) => _scores.Add(d))
            .ReturnsAsync(new ProposalReceipt(Guid.NewGuid(), "+5 a Marco"));
        sink.Setup(s => s.ProposeTurnAsync(It.IsAny<TurnProposalDraft>(), It.IsAny<CancellationToken>()))
            .Callback<TurnProposalDraft, CancellationToken>((d, _) => _turns.Add(d))
            .ReturnsAsync(new ProposalReceipt(Guid.NewGuid(), "passa il turno"));
        return new SessionAssistantToolset(_reader.Object, sink.Object, _matcher.Object);
    }

    private static AssistantSessionState State(
        string status = "InProgress", IReadOnlyList<string>? dimensions = null, Guid? gameId = null) => new(
        GameId: gameId ?? Guid.Parse("00000000-0000-0000-0000-0000000000aa"),
        GameName: "Azul",
        Status: status,
        CanRecordScores: status is "InProgress" or "Paused",
        CanAdvanceTurn: status == "InProgress",
        Players: [new(Marco, "Marco", 7, true), new(Giulia, "Giulia", 12, true), new(Luca, "Luca", 0, true)],
        CurrentTurnPlayerId: Luca,
        CurrentTurnIndex: 5,
        CurrentRound: AssistantRound.Current(5, 3),
        CurrentPhase: null,
        Dimensions: dimensions ?? ["punti"],
        Scores: [new(Marco, 2, "punti", 7)],
        HouseRules: []);

    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(2, 3, 1)]
    [InlineData(3, 3, 2)]
    [InlineData(5, 3, 2)]
    [InlineData(6, 3, 3)]
    [InlineData(4, 0, 1)]
    public void Round_IsAFullTurnOfTheTable(int turnIndex, int activePlayers, int expected) =>
        AssistantRound.Current(turnIndex, activePlayers).Should().Be(expected);

    [Fact]
    public async Task ProposeScore_ResolvesPlayerAndCurrentRound_AndRecordsTheDeltaWithTheCellBefore()
    {
        var result = await Toolset().ProposeScoreAsync("Marco", 5, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<ProposalCreated>();
        _scores.Should().ContainSingle().Which.Should().Be(new ScoreProposalDraft(Marco, "Marco", "punti", 2, 5, 7));
    }

    [Fact]
    public async Task ProposeScore_NegativePoints_IsAValidDelta()
    {
        await Toolset().ProposeScoreAsync("Marco", -2, cancellationToken: TestContext.Current.CancellationToken);

        _scores.Should().ContainSingle().Which.Points.Should().Be(-2);
    }

    [Fact]
    public async Task ProposeScore_UsesTheMatcherNotAnExactComparison()
    {
        _matcher.Setup(m => m.Match(" marco ", It.IsAny<IReadOnlyList<AssistantPlayer>>()))
            .Returns(new AssistantPlayerMatch(Marco, "Marco", false, []));

        await Toolset().ProposeScoreAsync(" marco ", 3, cancellationToken: TestContext.Current.CancellationToken);

        _scores.Should().ContainSingle().Which.PlayerId.Should().Be(Marco);
    }

    [Fact]
    public async Task ProposeScore_EmptyName_IsATypedError()
    {
        var toolset = Toolset();

        var result = await toolset.ProposeScoreAsync("  ", 3, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<AssistantToolError>().Which.Error.Should().Be("giocatore_mancante");
        _scores.Should().BeEmpty();
        _matcher.Verify(m => m.Match(It.IsAny<string>(), It.IsAny<IReadOnlyList<AssistantPlayer>>()), Times.Never,
            "il servizio di risoluzione lancia su un nome vuoto");
    }

    [Fact]
    public async Task ProposeScore_AmbiguousPlayer_ReturnsTheCandidates_WithoutAProposal()
    {
        _matcher.Setup(m => m.Match("Ma", It.IsAny<IReadOnlyList<AssistantPlayer>>()))
            .Returns(new AssistantPlayerMatch(null, null, true, ["Marco", "Mara"]));
        var toolset = Toolset();

        var result = await toolset.ProposeScoreAsync("Ma", 3, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<AssistantToolError>().Which.Candidates.Should().Equal("Marco", "Mara");
        _scores.Should().BeEmpty();
        toolset.ErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task ProposeScore_SeveralDimensionsAndNoneGiven_AsksWhichOne()
    {
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(dimensions: ["punti", "monete"]));

        var result = await Toolset().ProposeScoreAsync("Marco", 3, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<AssistantToolError>().Which.Error.Should().Be("dimensione_richiesta");
        _scores.Should().BeEmpty();
    }

    [Fact]
    public async Task ProposeScore_SessionCompleted_IsRefused()
    {
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(status: "Completed"));

        var result = await Toolset().ProposeScoreAsync("Marco", 3, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<AssistantToolError>().Which.Error.Should().Be("sessione_non_modificabile");
        _scores.Should().BeEmpty();
    }

    [Fact]
    public async Task ProposeTurn_RecordsTheExpectedTurn_AndIsRefusedWhenPaused()
    {
        await Toolset().ProposeTurnAsync(TestContext.Current.CancellationToken);
        _turns.Should().ContainSingle().Which.Should().Be(new TurnProposalDraft(5, Luca));

        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(State(status: "Paused"));
        var paused = await Toolset().ProposeTurnAsync(TestContext.Current.CancellationToken);
        paused.Should().BeOfType<AssistantToolError>();
    }

    [Fact]
    public void CreateTools_NamesAndRequiredParameters()
    {
        var tools = Toolset().CreateTools(includeRules: true).OfType<AIFunctionDeclaration>().ToList();

        tools.Select(t => t.Name).Should().Equal("stato_partita", "cerca_regole", "proponi_punteggio", "proponi_turno");
        var required = tools.Single(t => t.Name == "proponi_punteggio").JsonSchema.GetProperty("required")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        required.Should().BeEquivalentTo(["giocatore", "punti"]);
    }

    [Fact]
    public void CreateTools_WithoutAGame_DoesNotOfferTheRules() =>
        Toolset().CreateTools(includeRules: false).Select(t => t.Name).Should().NotContain("cerca_regole");
}
