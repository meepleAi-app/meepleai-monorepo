using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Spec §7.1: l'esecutore usa il ciclo vero, con un gateway finto che recita il modello.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4240")]
public sealed class SessionAssistantEvaluatorTests
{
    private static readonly AssistantModelSettings Settings = new("deepseek-flash", null, 0.2, 600, 4, null, RequestSource.Evaluation);

    private static AssistantScenario Scenario(params ExpectedProposal[] proposals) => new(
        Id: "it-score-simple", Language: "it", Category: "punteggio",
        State: new AssistantScenarioState(
            Game: "Azul", HasRules: true, Status: "InProgress", Players: ["Marco", "Giulia", "Luca"],
            CurrentTurnIndex: 4, CurrentPlayer: "Luca", Phase: null, Dimensions: ["punti"],
            Scores: [new AssistantScenarioScore("Marco", 1, "punti", 7)], HouseRules: []),
        Passages: [],
        Utterance: "Marco ha fatto 5 punti",
        Expected: new AssistantExpectation(["proponi_punteggio"], proposals));

    [Fact]
    public async Task Run_ModelProposesTheRightScore_Passes()
    {
        var gateway = Script(
            Calls(("proponi_punteggio", """{"giocatore":"Marco","punti":5}""")),
            Answer("Ho preparato la proposta: +5 a Marco. Confermala."));

        var result = await Evaluator(gateway).RunAsync(
            Scenario(new ExpectedProposal("Score", "Marco", "punti", 2, 5)), Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.Passed.Should().BeTrue();
        result.ToolsCalled.Should().Equal("proponi_punteggio");
        result.RoundTrips.Should().Be(2);
    }

    [Fact]
    public async Task Run_ModelClaimsTheActionWithoutATool_FailsWithClaimAndMissingProposal()
    {
        var gateway = Script(Answer("Ho segnato 5 punti a Marco."));

        var result = await Evaluator(gateway).RunAsync(
            Scenario(new ExpectedProposal("Score", "Marco", "punti", 2, 5)), Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.Passed.Should().BeFalse();
        result.ClaimedActionWithoutProposal.Should().BeTrue();
        result.MissingProposals.Should().Be(1);
        result.MissingTools.Should().Equal("proponi_punteggio");
    }

    [Fact]
    public async Task Run_ModelSaysAProposalIsReadyButNeverCreatedIt_IsAClaimWithoutProposal()
    {
        // Spike #4240, scenario en-turn: il modello legge lo stato e poi dichiara una proposta che non ha creato.
        var gateway = Script(
            Calls(("stato_partita", "{}")),
            Answer("Current turn: Luca (round 2). I've prepared a proposal to pass the turn to the next player — please confirm it."));

        var result = await Evaluator(gateway).RunAsync(Scenario(), Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.ClaimedActionWithoutProposal.Should().BeTrue("l'host cercherebbe una card che non esiste");
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Run_ModelSaysAProposalIsReadyAndItExists_IsNotAClaim()
    {
        var gateway = Script(
            Calls(("proponi_punteggio", """{"giocatore":"Marco","punti":5}""")),
            Answer("Ho preparato una proposta: +5 punti a Marco (round 2). Confermi?"));

        var result = await Evaluator(gateway).RunAsync(
            Scenario(new ExpectedProposal("Score", "Marco", "punti", 2, 5)), Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.ClaimedActionWithoutProposal.Should().BeFalse();
        result.Passed.Should().BeTrue();
    }

    // Il confronto delle proposte regge la soglia principale del gate («nessuna proposta non dovuta»): se
    // Extra tornasse sempre zero, lo spike direbbe «zero proposte non dovute» con qualunque modello.
    [Fact]
    public async Task Run_ModelProposesForTheWrongPlayer_IsAnExtraAndAMissingProposal()
    {
        var gateway = Script(
            Calls(("proponi_punteggio", """{"giocatore":"Giulia","punti":5}""")),
            Answer("Ho preparato una proposta: +5 punti a Giulia. Confermi?"));

        var result = await Evaluator(gateway).RunAsync(
            Scenario(new ExpectedProposal("Score", "Marco", "punti", 2, 5)), Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.ExtraProposals.Should().Be(1);
        result.MissingProposals.Should().Be(1);
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Run_ModelProposesTheSameScoreTwice_TheDuplicateIsAnExtraProposal()
    {
        var gateway = Script(
            Calls(("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""), ("proponi_punteggio", """{"giocatore":"Marco","punti":5}""")),
            Answer("Ho preparato la proposta: +5 a Marco. Confermi?"));

        var result = await Evaluator(gateway).RunAsync(
            Scenario(new ExpectedProposal("Score", "Marco", "punti", 2, 5)), Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.ExtraProposals.Should().Be(1, "l'host vedrebbe due card per gli stessi punti");
        result.MissingProposals.Should().Be(0);
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Run_ModelProposesWhenNothingIsExpected_IsAnExtraProposal()
    {
        var gateway = Script(Calls(("proponi_turno", "{}")), Answer("Ho preparato la proposta per passare il turno."));

        var result = await Evaluator(gateway).RunAsync(
            Scenario() with { Expected = new AssistantExpectation([], []) }, Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.ExtraProposals.Should().Be(1);
        result.MissingProposals.Should().Be(0);
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Run_ProposalsInAnotherOrderAndCase_StillMatch()
    {
        var gateway = Script(
            Calls(("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""), ("proponi_turno", "{}")),
            Answer("Ho preparato due proposte da confermare."));

        var result = await Evaluator(gateway).RunAsync(
            Scenario(new ExpectedProposal("AdvanceTurn", null, null, null, null), new ExpectedProposal("Score", "marco", "PUNTI", 2, 5)),
            Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.ExtraProposals.Should().Be(0);
        result.MissingProposals.Should().Be(0);
        result.Passed.Should().BeTrue();
    }

    // Chi scrive gli scenari non deve ricevere un 500 muto, né perdere i risultati degli altri scenari
    // della stessa richiesta: uno scenario che non si riesce a valutare è un risultato con il suo codice.
    [Fact]
    public async Task Run_ScenarioWithoutState_IsAnErrorResult_NotAnException()
    {
        var result = await Evaluator(Script(Answer("ok"))).RunAsync(
            Scenario() with { State = null! }, Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.Id.Should().Be("it-score-simple");
        result.Passed.Should().BeFalse();
        result.ErrorCode.Should().StartWith("evaluator_error:");
    }

    [Fact]
    public async Task Run_ScenarioWithoutExpectation_IsAnErrorResult_AndKeepsTheTurnCounters()
    {
        var result = await Evaluator(Script(Answer("Tocca a Luca."))).RunAsync(
            Scenario() with { Expected = null! }, Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.Passed.Should().BeFalse();
        result.ErrorCode.Should().StartWith("evaluator_error:");
        result.RoundTrips.Should().Be(1, "il turno è stato eseguito: i suoi contatori restano");
    }

    // #4256: il prompt arriva dalla richiesta, così una variante si misura senza ricostruire l'immagine.
    [Fact]
    public async Task Run_SendsThePromptSetItIsGiven_ToTheModel()
    {
        LlmChatRequest? sent = null;
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(Answer("Tocca a Luca."));

        await Evaluator(gateway).RunAsync(
            Scenario(), Settings, new AssistantPromptSet("PROMPT ITALIANO DI PROVA", "ENGLISH TEST PROMPT"),
            TestContext.Current.CancellationToken);

        sent!.Messages[0].Role.Should().Be(LlmChatRoles.System);
        sent.Messages[0].Content.Should().Be("PROMPT ITALIANO DI PROVA");
    }

    [Fact]
    public async Task Handler_PartialPromptOverride_KeepsTheDefaultForTheOtherLanguage()
    {
        var sent = new List<LlmChatRequest>();
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmChatRequest, CancellationToken>((request, _) => sent.Add(request))
            .ReturnsAsync(Answer("ok"));
        var handler = new RunSessionAssistantEvaluationCommandHandler(Evaluator(gateway));
        var english = Scenario() with { Id = "en-state", Language = "en" };

        var report = await handler.Handle(
            new RunSessionAssistantEvaluationCommand(
                new AssistantScenarioSet(2, [Scenario(), english]), "deepseek-flash", null, 0.2, 600, 4,
                new AssistantPromptOverride(Italian: "PROMPT ITALIANO DI PROVA", English: null)),
            TestContext.Current.CancellationToken);

        report.Results.Should().HaveCount(2);
        sent[0].Messages[0].Content.Should().Be("PROMPT ITALIANO DI PROVA");
        sent[1].Messages[0].Content.Should().Be(SessionAssistantPrompts.English);
    }

    // Review di #4256: il file dei risultati deve dire quale prompt è stato USATO, non quale è stato mandato.
    // Un'immagine che non conosce il campo «prompts» lo scarta in silenzio e misura il predefinito.
    [Fact]
    public async Task Handler_ReportsTheFingerprintOfThePromptsItUsed()
    {
        var handler = new RunSessionAssistantEvaluationCommandHandler(Evaluator(Script(Answer("ok"), Answer("ok"))));
        var set = new AssistantScenarioSet(3, [Scenario()]);

        var byDefault = await handler.Handle(
            new RunSessionAssistantEvaluationCommand(set, "deepseek-flash", null, 0.2, 600, 4),
            TestContext.Current.CancellationToken);
        var overridden = await handler.Handle(
            new RunSessionAssistantEvaluationCommand(set, "deepseek-flash", null, 0.2, 600, 4,
                new AssistantPromptOverride(Italian: "PROMPT ITALIANO DI PROVA", English: null)),
            TestContext.Current.CancellationToken);

        byDefault.PromptFingerprint.Should().Be(
            new AssistantPromptFingerprint(Sha256(SessionAssistantPrompts.Italian), Sha256(SessionAssistantPrompts.English)));
        overridden.PromptFingerprint.Should().Be(
            new AssistantPromptFingerprint(Sha256("PROMPT ITALIANO DI PROVA"), Sha256(SessionAssistantPrompts.English)));
    }

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public async Task Run_GatewayRefuses_IsAnErrorResult_NotAnException()
    {
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("cap") with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["error_code"] = LlmBudgetAdmission.DailyBudgetErrorCode },
            });

        var result = await Evaluator(gateway).RunAsync(Scenario(), Settings, SessionAssistantPrompts.Default, TestContext.Current.CancellationToken);

        result.Passed.Should().BeFalse();
        result.ErrorCode.Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
    }

    [Fact]
    public void Report_AggregatesPassRateCostAndP95()
    {
        var results = Enumerable.Range(1, 20).Select(i => new AssistantScenarioResult(
            $"s{i}", "punteggio", Passed: i % 4 != 0, ToolsCalled: [], MissingTools: [], ExtraProposals: i == 4 ? 1 : 0,
            MissingProposals: 0, ClaimedActionWithoutProposal: false, ToolErrors: 0, InvalidArguments: i == 8 ? 2 : 0, RoundTrips: 2,
            CostUsd: 0.001m, LatencyMs: i * 100, ErrorCode: null, FinalText: "ok")).ToList();

        var report = AssistantEvaluationReport.From("deepseek-flash", results);

        report.Scenarios.Should().Be(20);
        report.Passed.Should().Be(15);
        report.ScenariosWithExtraProposals.Should().Be(1);
        report.InvalidArguments.Should().Be(2);
        report.AverageRoundTrips.Should().Be(2);
        report.AverageCostPerTurnUsd.Should().Be(0.001m);
        report.LatencyP95Ms.Should().Be(1900);
    }

    [Fact]
    public void ScenarioSet_InTheRepository_IsValidJson()
    {
        var path = Path.Combine(FindRepoRoot(), "tests", "llm-eval", "session-assistant", "scenarios.json");
        var set = JsonSerializer.Deserialize<AssistantScenarioSet>(File.ReadAllText(path), AssistantScenarioSet.Json);

        set!.Scenarios.Should().NotBeEmpty();
        set.Scenarios.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        set.Scenarios.Should().OnlyContain(s => s.Language == "it" || s.Language == "en");
    }

    private static SessionAssistantEvaluator Evaluator(Mock<ILlmService> gateway) =>
        new(new AssistantChatClientFactory(gateway.Object, NullLoggerFactory.Instance, new ServiceCollection().BuildServiceProvider()),
            new ExactNameMatcher(), NullLogger<SessionAssistantEvaluator>.Instance);

    private static Mock<ILlmService> Script(params LlmCompletionResult[] turns)
    {
        var gateway = new Mock<ILlmService>();
        var sequence = gateway.SetupSequence(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()));
        foreach (var turn in turns)
        {
            sequence = sequence.ReturnsAsync(turn);
        }

        return gateway;
    }

    private static LlmCompletionResult Calls(params (string Name, string Args)[] calls) =>
        LlmCompletionResult.CreateSuccess(string.Empty, new LlmUsage(100, 5, 105)) with
        {
            ToolCalls = calls.Select((c, i) => new LlmToolCall($"call_{i}", c.Name, c.Args)).ToList(),
        };

    private static LlmCompletionResult Answer(string text) => LlmCompletionResult.CreateSuccess(text, new LlmUsage(120, 10, 130));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests", "llm-eval")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("repository root with tests/llm-eval not found");
    }

    /// <summary>Un matcher minimo per i test dell'esecutore: confronto esatto, senza maiuscole.</summary>
    private sealed class ExactNameMatcher : IAssistantPlayerMatcher
    {
        public AssistantPlayerMatch Match(string name, IReadOnlyList<AssistantPlayer> players)
        {
            var found = players.Where(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            return found.Count == 1
                ? new AssistantPlayerMatch(found[0].Id, found[0].Name, false, [])
                : new AssistantPlayerMatch(null, null, found.Count > 1, found.Select(p => p.Name).ToList());
        }
    }
}
