#pragma warning disable MA0048 // File name must match type name - Contains related DTOs
using Api.BoundedContexts.GameToolkit.Domain.Enums;

namespace Api.BoundedContexts.GameToolkit.Application.DTOs;

internal record GameToolkitDto(
    Guid Id,
    Guid GameId,
    string Name,
    int Version,
    Guid CreatedByUserId,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<DiceToolDto> DiceTools,
    IReadOnlyList<CardToolDto> CardTools,
    IReadOnlyList<TimerToolDto> TimerTools,
    IReadOnlyList<CounterToolDto> CounterTools,
    ScoringTemplateDto? ScoringTemplate,
    TurnTemplateDto? TurnTemplate,
    string? StateTemplate,
    string? AgentConfig
);

internal record DiceToolDto(
    string Name,
    DiceType DiceType,
    int Quantity,
    string[]? CustomFaces,
    bool IsInteractive,
    string? Color
);

internal record CardToolDto(
    string Name,
    string DeckType,
    int CardCount,
    bool Shuffleable
);

internal record TimerToolDto(
    string Name,
    int DurationSeconds,
    bool IsCountdown,
    bool AutoStart,
    string? Color
);

internal record CounterToolDto(
    string Name,
    int MinValue,
    int MaxValue,
    int DefaultValue,
    bool IsPerPlayer,
    string? Icon,
    string? Color
);

internal record ScoringTemplateDto(
    string[] Dimensions,
    string DefaultUnit,
    ScoreType ScoreType
);

internal record TurnTemplateDto(
    TurnOrderType TurnOrderType,
    string[] Phases
);

// Request DTOs
internal record CreateToolkitRequest(
    Guid GameId,
    string Name
);

internal record UpdateToolkitRequest(
    string? Name
);

internal record AddDiceToolRequest(
    string Name,
    DiceType DiceType,
    int Quantity = 1,
    string[]? CustomFaces = null,
    bool IsInteractive = true,
    string? Color = null
);

internal record AddCounterToolRequest(
    string Name,
    int MinValue = 0,
    int MaxValue = 999,
    int DefaultValue = 0,
    bool IsPerPlayer = false,
    string? Icon = null,
    string? Color = null
);

internal record SetScoringTemplateRequest(
    string[] Dimensions,
    string DefaultUnit = "points",
    ScoreType ScoreType = ScoreType.Points
);

internal record SetTurnTemplateRequest(
    TurnOrderType TurnOrderType = TurnOrderType.RoundRobin,
    string[]? Phases = null
);
