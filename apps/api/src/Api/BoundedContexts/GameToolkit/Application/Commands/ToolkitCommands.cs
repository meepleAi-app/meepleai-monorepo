#pragma warning disable MA0048 // File name must match type name - Contains related commands
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

internal record CreateToolkitCommand(
    Guid GameId,
    string Name,
    Guid CreatedByUserId
) : ICommand<GameToolkitDto>;

internal record UpdateToolkitCommand(
    Guid ToolkitId,
    string? Name
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
