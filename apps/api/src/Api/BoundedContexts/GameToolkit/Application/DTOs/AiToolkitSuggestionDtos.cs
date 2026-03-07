#pragma warning disable MA0048 // File name must match type name - Contains related DTOs
using Api.BoundedContexts.GameToolkit.Domain.Enums;

namespace Api.BoundedContexts.GameToolkit.Application.DTOs;

/// <summary>
/// Structured response from LLM for auto-generating a GameToolkit.
/// Used with ILlmService.GenerateJsonAsync&lt;AiToolkitSuggestionDto&gt;().
/// </summary>
internal record AiToolkitSuggestionDto(
    string ToolkitName,
    List<AiDiceToolSuggestion> DiceTools,
    List<AiCounterToolSuggestion> CounterTools,
    List<AiTimerToolSuggestion> TimerTools,
    AiScoringTemplateSuggestion? ScoringTemplate,
    AiTurnTemplateSuggestion? TurnTemplate,
    AiOverrideSuggestion? Overrides,
    string Reasoning
);

internal record AiDiceToolSuggestion(
    string Name,
    DiceType DiceType,
    int Quantity,
    string[]? CustomFaces,
    bool IsInteractive,
    string? Color
);

internal record AiCounterToolSuggestion(
    string Name,
    int MinValue,
    int MaxValue,
    int DefaultValue,
    bool IsPerPlayer,
    string? Icon,
    string? Color
);

internal record AiTimerToolSuggestion(
    string Name,
    int DurationSeconds,
    TimerType TimerType,
    bool AutoStart,
    string? Color,
    bool IsPerPlayer,
    int? WarningThresholdSeconds
);

internal record AiScoringTemplateSuggestion(
    string[] Dimensions,
    string DefaultUnit,
    ScoreType ScoreType
);

internal record AiTurnTemplateSuggestion(
    TurnOrderType TurnOrderType,
    string[] Phases
);

internal record AiOverrideSuggestion(
    bool OverridesTurnOrder,
    bool OverridesScoreboard,
    bool OverridesDiceSet
);
