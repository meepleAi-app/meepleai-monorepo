using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
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

    private SessionAssistantToolset Toolset(IAssistantRuleSource? rules = null)
    {
        var sink = new Mock<IAssistantProposalSink>();
        sink.Setup(s => s.ProposeScoreAsync(It.IsAny<ScoreProposalDraft>(), It.IsAny<CancellationToken>()))
            .Callback<ScoreProposalDraft, CancellationToken>((d, _) => _scores.Add(d))
            .ReturnsAsync(new ProposalReceipt(Guid.NewGuid(), "+5 a Marco"));
        sink.Setup(s => s.ProposeTurnAsync(It.IsAny<TurnProposalDraft>(), It.IsAny<CancellationToken>()))
            .Callback<TurnProposalDraft, CancellationToken>((d, _) => _turns.Add(d))
            .ReturnsAsync(new ProposalReceipt(Guid.NewGuid(), "passa il turno"));
        return new SessionAssistantToolset(_reader.Object, sink.Object, _matcher.Object, rules);
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

    /// <summary>
    /// L'indice di turno del dominio vale 0 prima dell'avvio e parte da 1 con <c>Start()</c>
    /// (<c>LiveGameSession.GetCurrentTurnPlayerId</c> usa <c>(indice - 1) % n</c>). Con tre giocatori i
    /// turni 1, 2 e 3 sono il primo giro: con la formula a base 0 l'ultimo giocatore cadeva nel round 2.
    /// </summary>
    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(1, 3, 1)]
    [InlineData(3, 3, 1)]
    [InlineData(4, 3, 2)]
    [InlineData(6, 3, 2)]
    [InlineData(7, 3, 3)]
    [InlineData(4, 0, 1)]
    [InlineData(-2, 3, 1)]
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

    /// <summary>
    /// Spec §6.3: argomenti non validi sono un errore tipizzato che il modello può leggere, non
    /// un'eccezione dentro lo strumento. Il limite è quello dell'aggregato (scostamento S14).
    /// </summary>
    [Theory]
    [InlineData(100_001)]
    [InlineData(-100_001)]
    [InlineData(int.MinValue)]
    public async Task ProposeScore_PointsBeyondTheLimit_IsATypedError(int points)
    {
        var toolset = Toolset();

        var result = await toolset.ProposeScoreAsync("Marco", points, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeOfType<AssistantToolError>().Which.Error.Should().Be("punti_fuori_limite");
        toolset.ErrorCount.Should().Be(1);
        _scores.Should().BeEmpty();
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
        var tools = Toolset(Mock.Of<IAssistantRuleSource>()).CreateTools().OfType<AIFunctionDeclaration>().ToList();

        tools.Select(t => t.Name).Should().Equal("stato_partita", "cerca_regole", "proponi_punteggio", "proponi_turno");
        var required = tools.Single(t => t.Name == "proponi_punteggio").JsonSchema.GetProperty("required")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        required.Should().BeEquivalentTo(["giocatore", "punti"]);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ChunkCitation Cite(string text, int page = 3, string doc = "pdf-1") =>
        new(doc, page, 0.9f, text, CopyrightTier.Full);

    private static IAssistantRuleSource Rules(params AssistantRuleSearch[] answers)
    {
        var queue = new Queue<AssistantRuleSearch>(answers);
        var rules = new Mock<IAssistantRuleSource>();
        rules.Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => queue.Dequeue());
        return rules.Object;
    }

    private static AssistantRuleSearch Found(params ChunkCitation[] citations) =>
        new(citations.Select(c => new AssistantRulePassage(c.SnippetPreview, c.PageNumber)).ToList(), citations);

    // #4411: cerca_regole c'è solo se il turno ha una sorgente del regolamento (una sessione senza gioco non ne ha).
    [Fact]
    public void CreateTools_OffersTheRules_OnlyWithARuleSource()
    {
        Toolset().CreateTools().Select(t => t.Name).Should().NotContain(SessionAssistantToolset.CercaRegole);
        Toolset(Rules()).CreateTools().Select(t => t.Name).Should().Contain(SessionAssistantToolset.CercaRegole);
    }

    [Fact]
    public async Task SearchRules_ReturnsThePassages_AndCollectsTheCitations()
    {
        var toolset = Toolset(Rules(Found(Cite("Si pescano 4 tessere."))));

        var result = await toolset.SearchRulesAsync("quante tessere?", Ct);

        result.Should().BeOfType<RulePassagesFound>()
            .Which.Passaggi.Should().ContainSingle().Which.Should().Be(new AssistantRulePassage("Si pescano 4 tessere.", 3));
        toolset.Citations.Should().ContainSingle();
        toolset.ErrorCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchRules_FindingNothing_SaysSo_WithoutAnError()
    {
        var toolset = Toolset(Rules(new AssistantRuleSearch([], [])));

        var result = await toolset.SearchRulesAsync("quanti punti vale una riga?", Ct);

        var found = result.Should().BeOfType<RulePassagesFound>().Which;
        found.Passaggi.Should().BeEmpty();
        found.Nota.Should().Contain("non rispondere a memoria");
        toolset.ErrorCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchRules_WithoutAccess_IsATypedError()
    {
        var toolset = Toolset(Rules(AssistantRuleSearch.NotAccessible));

        var result = await toolset.SearchRulesAsync("quante tessere?", Ct);

        result.Should().BeOfType<AssistantToolError>().Which.Error.Should().Be("regolamento_non_accessibile");
        toolset.Citations.Should().BeEmpty();
        toolset.ErrorCount.Should().Be(1);
    }

    // Review Focus 5 del piano 3.
    [Fact]
    public async Task SearchRules_WithAnEmptyQuestion_IsATypedError_AndDoesNotSearch()
    {
        var rules = new Mock<IAssistantRuleSource>(MockBehavior.Strict);
        var toolset = Toolset(rules.Object);

        var result = await toolset.SearchRulesAsync("  ", Ct);

        result.Should().BeOfType<AssistantToolError>().Which.Error.Should().Be("domanda_mancante");
    }

    // Review Focus 1 del piano 3: due chiamate che trovano lo stesso passaggio danno una sola fonte.
    [Fact]
    public async Task SearchRules_TheSamePassageTwice_IsCitedOnce()
    {
        var toolset = Toolset(Rules(Found(Cite("Si pescano 4 tessere.")), Found(Cite("Si pescano 4 tessere."), Cite("Altro.", 5))));

        await toolset.SearchRulesAsync("quante tessere?", Ct);
        await toolset.SearchRulesAsync("quante tessere per espositore?", Ct);

        toolset.Citations.Select(c => (c.PageNumber, c.SnippetPreview)).Should().Equal((3, "Si pescano 4 tessere."), (5, "Altro."));
    }
}
