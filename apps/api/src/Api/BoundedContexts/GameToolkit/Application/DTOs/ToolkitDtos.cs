#pragma warning disable MA0048 // File name must match type name - Contains related DTOs
using Api.BoundedContexts.GameToolkit.Domain.Enums;

namespace Api.BoundedContexts.GameToolkit.Application.DTOs;

internal record GameToolkitDto(
    Guid Id,
    Guid? GameId,
    Guid? PrivateGameId,
    string Name,
    int Version,
    Guid CreatedByUserId,
    bool IsPublished,
    bool OverridesTurnOrder,
    bool OverridesScoreboard,
    bool OverridesDiceSet,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<DiceToolDto> DiceTools,
    IReadOnlyList<CardToolDto> CardTools,
    IReadOnlyList<TimerToolDto> TimerTools,
    IReadOnlyList<CounterToolDto> CounterTools,
    ScoringTemplateDto? ScoringTemplate,
    TurnTemplateDto? TurnTemplate,
    StateTemplateDto? StateTemplate,
    string? AgentConfig,
    TemplateStatus TemplateStatus = TemplateStatus.Draft,
    bool IsTemplate = false,
    string? ReviewNotes = null,
    Guid? ReviewedByUserId = null,
    DateTime? ReviewedAt = null
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
    bool Shuffleable,
    CardZone DefaultZone,
    CardOrientation DefaultOrientation,
    IReadOnlyList<CardEntryDto> CardEntries,
    bool AllowDraw,
    bool AllowDiscard,
    bool AllowPeek,
    bool AllowReturnToDeck
);

internal record CardEntryDto(
    string Name,
    string? Suit,
    string? Rank,
    string? CustomData
);

internal record TimerToolDto(
    string Name,
    int DurationSeconds,
    TimerType TimerType,
    bool AutoStart,
    string? Color,
    bool IsPerPlayer,
    int? WarningThresholdSeconds
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

internal record StateTemplateDto(
    string Name,
    string? Description,
    TemplateCategory Category,
    string SchemaJson
);

// Request DTOs
internal record CreateToolkitRequest(
    Guid? GameId,
    Guid? PrivateGameId,
    string Name,
    bool OverridesTurnOrder = false,
    bool OverridesScoreboard = false,
    bool OverridesDiceSet = false
);

internal record UpdateToolkitRequest(
    string? Name,
    bool? OverridesTurnOrder = null,
    bool? OverridesScoreboard = null,
    bool? OverridesDiceSet = null
);

internal record AddDiceToolRequest(
    string Name,
    DiceType DiceType,
    int Quantity = 1,
    string[]? CustomFaces = null,
    bool IsInteractive = true,
    string? Color = null
);

internal record AddCardToolRequest(
    string Name,
    string DeckType = "standard",
    int CardCount = 52,
    bool Shuffleable = true,
    CardZone DefaultZone = CardZone.DrawPile,
    CardOrientation DefaultOrientation = CardOrientation.FaceDown,
    IReadOnlyList<CardEntryDto>? CardEntries = null,
    bool AllowDraw = true,
    bool AllowDiscard = true,
    bool AllowPeek = false,
    bool AllowReturnToDeck = false
);

internal record AddTimerToolRequest(
    string Name,
    int DurationSeconds,
    TimerType TimerType = TimerType.CountDown,
    bool AutoStart = false,
    string? Color = null,
    bool IsPerPlayer = false,
    int? WarningThresholdSeconds = null
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

internal record SetStateTemplateRequest(
    string Name,
    TemplateCategory Category,
    string SchemaJson,
    string? Description = null
);

internal record ApplyAiSuggestionRequest(
    Guid? ToolkitId,
    AiToolkitSuggestionDto Suggestion
);

// Template marketplace request DTOs
internal record ApproveTemplateRequest(string? Notes);
internal record RejectTemplateRequest(string Notes);
internal record CloneFromTemplateRequest(Guid GameId);
