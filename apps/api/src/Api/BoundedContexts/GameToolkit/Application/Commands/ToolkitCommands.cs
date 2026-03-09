#pragma warning disable MA0048 // File name must match type name - Contains related commands
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

internal record CreateToolkitCommand(
    Guid? GameId,
    string Name,
    Guid CreatedByUserId,
    Guid? PrivateGameId = null,
    bool OverridesTurnOrder = false,
    bool OverridesScoreboard = false,
    bool OverridesDiceSet = false
) : ICommand<GameToolkitDto>;

internal record UpdateToolkitCommand(
    Guid ToolkitId,
    string? Name,
    bool? OverridesTurnOrder = null,
    bool? OverridesScoreboard = null,
    bool? OverridesDiceSet = null
) : ICommand<GameToolkitDto>;

internal record PublishToolkitCommand(
    Guid ToolkitId
) : ICommand<GameToolkitDto>;

internal record AddDiceToolCommand(
    Guid ToolkitId,
    string Name,
    DiceType DiceType,
    int Quantity = 1,
    string[]? CustomFaces = null,
    bool IsInteractive = true,
    string? Color = null
) : ICommand<GameToolkitDto>;

internal record AddCardToolCommand(
    Guid ToolkitId,
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
) : ICommand<GameToolkitDto>;

internal record AddTimerToolCommand(
    Guid ToolkitId,
    string Name,
    int DurationSeconds,
    TimerType TimerType = TimerType.CountDown,
    bool AutoStart = false,
    string? Color = null,
    bool IsPerPlayer = false,
    int? WarningThresholdSeconds = null
) : ICommand<GameToolkitDto>;

internal record AddCounterToolCommand(
    Guid ToolkitId,
    string Name,
    int MinValue = 0,
    int MaxValue = 999,
    int DefaultValue = 0,
    bool IsPerPlayer = false,
    string? Icon = null,
    string? Color = null
) : ICommand<GameToolkitDto>;

internal record RemoveDiceToolCommand(
    Guid ToolkitId,
    string ToolName
) : ICommand<GameToolkitDto>;

internal record RemoveCardToolCommand(
    Guid ToolkitId,
    string ToolName
) : ICommand<GameToolkitDto>;

internal record RemoveTimerToolCommand(
    Guid ToolkitId,
    string ToolName
) : ICommand<GameToolkitDto>;

internal record RemoveCounterToolCommand(
    Guid ToolkitId,
    string ToolName
) : ICommand<GameToolkitDto>;

internal record SetScoringTemplateCommand(
    Guid ToolkitId,
    string[] Dimensions,
    string DefaultUnit = "points",
    ScoreType ScoreType = ScoreType.Points
) : ICommand<GameToolkitDto>;

internal record SetTurnTemplateCommand(
    Guid ToolkitId,
    TurnOrderType TurnOrderType = TurnOrderType.RoundRobin,
    string[]? Phases = null
) : ICommand<GameToolkitDto>;

internal record SetStateTemplateCommand(
    Guid ToolkitId,
    string Name,
    TemplateCategory Category,
    string SchemaJson,
    string? Description = null
) : ICommand<GameToolkitDto>;

internal record ClearStateTemplateCommand(
    Guid ToolkitId
) : ICommand<GameToolkitDto>;

// Template marketplace commands
internal record SubmitTemplateForReviewCommand(
    Guid ToolkitId,
    Guid UserId
) : ICommand<GameToolkitDto>;

internal record ApproveTemplateCommand(
    Guid ToolkitId,
    Guid AdminUserId,
    string? Notes
) : ICommand<GameToolkitDto>;

internal record RejectTemplateCommand(
    Guid ToolkitId,
    Guid AdminUserId,
    string Notes
) : ICommand<GameToolkitDto>;

internal record CloneFromTemplateCommand(
    Guid TemplateId,
    Guid GameId,
    Guid UserId
) : ICommand<GameToolkitDto>;
