using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>
/// Spec §7.1: il set di scenari arriva dal corpo della richiesta. Un set malformato è un errore di
/// validazione (422) che nomina lo scenario e il problema, mai un'eccezione (500).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4240")]
public sealed class RunSessionAssistantEvaluationCommandValidatorTests
{
    private readonly RunSessionAssistantEvaluationCommandValidator _validator = new();

    private static AssistantScenario Scenario() => new(
        Id: "it-score-simple", Language: "it", Category: "punteggio",
        State: new AssistantScenarioState(
            Game: "Azul", HasRules: true, Status: "InProgress", Players: ["Marco", "Giulia", "Luca"],
            CurrentTurnIndex: 4, CurrentPlayer: "Luca", Phase: null, Dimensions: ["punti"],
            Scores: [new AssistantScenarioScore("Marco", 1, "punti", 7)], HouseRules: []),
        Passages: [new AssistantScenarioPassage("Quattro tessere per espositore.", 3)],
        Utterance: "Marco ha fatto 5 punti",
        Expected: new AssistantExpectation(["proponi_punteggio"], [new ExpectedProposal("Score", "Marco", "punti", 2, 5)]));

    private static RunSessionAssistantEvaluationCommand Command(AssistantScenarioSet set) =>
        new(set, "deepseek-flash", null, 0.2, 600, 4);

    private static RunSessionAssistantEvaluationCommand Command(params AssistantScenario[] scenarios) =>
        Command(new AssistantScenarioSet(1, scenarios));

    [Fact]
    public void WellFormedScenario_IsValid() =>
        _validator.Validate(Command(Scenario())).IsValid.Should().BeTrue();

    [Fact]
    public void ScenarioListMissing_IsAValidationError_NotAnException()
    {
        // {"scenarios":{"version":1}}: il binding lascia la lista a null.
        var result = _validator.Validate(Command(new AssistantScenarioSet(1, null!)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Scenarios.Scenarios");
    }

    [Fact]
    public void EmptyScenarioList_IsAValidationError() =>
        _validator.Validate(Command()).IsValid.Should().BeFalse();

    [Fact]
    public void NullScenario_IsAValidationError_NotAnException() =>
        _validator.Validate(Command(Scenario(), null!)).IsValid.Should().BeFalse();

    public static TheoryData<string, string> MalformedScenarios() => new()
    {
        { "state", """{"id":"s1","language":"it","category":"x","passages":[],"utterance":"ciao","expected":{"tools":[],"proposals":[]}}""" },
        { "expected", """{"id":"s1","language":"it","category":"x","state":{"game":"Azul","hasRules":true,"status":"InProgress","players":["Marco"],"currentTurnIndex":0,"dimensions":["punti"],"scores":[],"houseRules":[]},"passages":[],"utterance":"ciao"}""" },
        { "utterance", """{"id":"s1","language":"it","category":"x","state":{"game":"Azul","hasRules":true,"status":"InProgress","players":["Marco"],"currentTurnIndex":0,"dimensions":["punti"],"scores":[],"houseRules":[]},"passages":[],"utterance":" ","expected":{"tools":[],"proposals":[]}}""" },
        { "state.scores", """{"id":"s1","language":"it","category":"x","state":{"game":"Azul","hasRules":true,"status":"InProgress","players":["Marco"],"currentTurnIndex":0,"dimensions":["punti"],"houseRules":[]},"passages":[],"utterance":"ciao","expected":{"tools":[],"proposals":[]}}""" },
        { "state.houseRules", """{"id":"s1","language":"it","category":"x","state":{"game":"Azul","hasRules":true,"status":"InProgress","players":["Marco"],"currentTurnIndex":0,"dimensions":["punti"],"scores":[]},"passages":[],"utterance":"ciao","expected":{"tools":[],"proposals":[]}}""" },
        { "passages", """{"id":"s1","language":"it","category":"x","state":{"game":"Azul","hasRules":true,"status":"InProgress","players":["Marco"],"currentTurnIndex":0,"dimensions":["punti"],"scores":[],"houseRules":[]},"utterance":"ciao","expected":{"tools":[],"proposals":[]}}""" },
        { "proponi_punti", """{"id":"s1","language":"it","category":"x","state":{"game":"Azul","hasRules":true,"status":"InProgress","players":["Marco"],"currentTurnIndex":0,"dimensions":["punti"],"scores":[],"houseRules":[]},"passages":[],"utterance":"ciao","expected":{"tools":["proponi_punti"],"proposals":[]}}""" },
        { "Punteggio", """{"id":"s1","language":"it","category":"x","state":{"game":"Azul","hasRules":true,"status":"InProgress","players":["Marco"],"currentTurnIndex":0,"dimensions":["punti"],"scores":[],"houseRules":[]},"passages":[],"utterance":"ciao","expected":{"tools":[],"proposals":[{"kind":"Punteggio","player":"Marco","dimension":"punti","round":1,"points":5}]}}""" },
        { "points", """{"id":"s1","language":"it","category":"x","state":{"game":"Azul","hasRules":true,"status":"InProgress","players":["Marco"],"currentTurnIndex":0,"dimensions":["punti"],"scores":[],"houseRules":[]},"passages":[],"utterance":"ciao","expected":{"tools":[],"proposals":[{"kind":"Score","player":"Marco","dimension":"punti","round":1}]}}""" },
        { "language", """{"id":"s1","language":"de","category":"x","state":{"game":"Azul","hasRules":true,"status":"InProgress","players":["Marco"],"currentTurnIndex":0,"dimensions":["punti"],"scores":[],"houseRules":[]},"passages":[],"utterance":"ciao","expected":{"tools":[],"proposals":[]}}""" },
    };

    [Theory]
    [MemberData(nameof(MalformedScenarios))]
    public void MalformedScenario_IsAValidationError_ThatNamesTheScenarioAndTheProblem(string problem, string json)
    {
        // Dal JSON e non da un record costruito a mano: è il binding a lasciare a null ciò che manca.
        var scenario = JsonSerializer.Deserialize<AssistantScenario>(json, AssistantScenarioSet.Json)!;

        var result = _validator.Validate(Command(Scenario(), scenario));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("s1").And.Contain(problem);
    }

    [Fact]
    public void TheRepositoryScenarioSet_IsValid()
    {
        var path = Path.Combine(FindRepoRoot(), "tests", "llm-eval", "session-assistant", "scenarios.json");
        var set = JsonSerializer.Deserialize<AssistantScenarioSet>(File.ReadAllText(path), AssistantScenarioSet.Json)!;

        var result = _validator.Validate(Command(set));

        result.Errors.Select(e => e.ErrorMessage).Should().BeEmpty();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests", "llm-eval")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Radice del repository non trovata.");
    }
}
