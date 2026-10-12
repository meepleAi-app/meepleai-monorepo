using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable MA0048 // File name must match type name - scenario file model
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Il set di scenari dello spike (spec §7.1), in <c>tests/llm-eval/session-assistant/scenarios.json</c>.</summary>
internal sealed record AssistantScenarioSet(int Version, IReadOnlyList<AssistantScenario> Scenarios)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

internal sealed record AssistantScenario(
    string Id,
    string Language,
    string Category,
    AssistantScenarioState State,
    IReadOnlyList<AssistantScenarioPassage> Passages,
    string Utterance,
    AssistantExpectation Expected);

internal sealed record AssistantScenarioState(
    string Game,
    bool HasRules,
    string Status,
    IReadOnlyList<string> Players,
    int CurrentTurnIndex,
    string? CurrentPlayer,
    string? Phase,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<AssistantScenarioScore> Scores,
    IReadOnlyList<string> HouseRules);

internal sealed record AssistantScenarioScore(string Player, int Round, string Dimension, int Value);

internal sealed record AssistantScenarioPassage(string Text, int? Page);

/// <summary>Strumenti che devono essere chiamati (altri sono ammessi) e proposte attese, esatte e senza ordine.</summary>
internal sealed record AssistantExpectation(IReadOnlyList<string> Tools, IReadOnlyList<ExpectedProposal> Proposals);

/// <summary><c>Kind</c> è <c>Score</c> o <c>AdvanceTurn</c>; per <c>AdvanceTurn</c> gli altri campi sono nulli.</summary>
internal sealed record ExpectedProposal(string Kind, string? Player, string? Dimension, int? Round, int? Points);
