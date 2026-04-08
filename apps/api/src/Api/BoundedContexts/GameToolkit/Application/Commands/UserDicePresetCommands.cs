#pragma warning disable MA0048 // File name must match type name - Contains related commands
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

internal record AddUserDicePresetCommand(
    Guid ToolkitId,
    Guid UserId,
    string Name,
    string Formula
) : ICommand<GameToolkitDto>;

internal record RemoveUserDicePresetCommand(
    Guid ToolkitId,
    Guid UserId,
    string PresetName
) : ICommand<GameToolkitDto>;
