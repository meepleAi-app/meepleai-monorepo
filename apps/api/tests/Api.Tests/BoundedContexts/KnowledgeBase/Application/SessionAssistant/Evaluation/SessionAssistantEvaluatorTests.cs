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
            Scenario(new ExpectedProposal("Score", "Marco", "punti", 2, 5)), Settings, TestContext.Current.CancellationToken);

        result.Passed.Should().BeTrue();
        result.ToolsCalled.Should().Equal("proponi_punteggio");
        result.RoundTrips.Should().Be(2);
    }

    [Fact]
    public async Task Run_ModelClaimsTheActionWithoutATool_FailsWithClaimAndMissingProposal()
    {
        var gateway = Script(Answer("Ho segnato 5 punti a Marco."));

        var result = await Evaluator(gateway).RunAsync(
            Scenario(new ExpectedProposal("Score", "Marco", "punti", 2, 5)), Settings, TestContext.Current.CancellationToken);

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

        var result = await Evaluator(gateway).RunAsync(Scenario(), Settings, TestContext.Current.CancellationToken);

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
            Scenario(new ExpectedProposal("Score", "Marco", "punti", 2, 5)), Settings, TestContext.Current.CancellationToken);

        result.ClaimedActionWithoutProposal.Should().BeFalse();
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task Run_GatewayRefuses_IsAnErrorResult_NotAnException()
    {
        var gateway = new Mock<ILlmService>();
        gateway.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("cap") with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["error_code"] = LlmBudgetAdmission.DailyBudgetErrorCode },
            });

        var result = await Evaluator(gateway).RunAsync(Scenario(), Settings, TestContext.Current.CancellationToken);

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
            new ExactNameMatcher());

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
