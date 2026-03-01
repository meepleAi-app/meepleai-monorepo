using Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.ToolState;

/// <summary>
/// Initializes all tool states for a session from its linked toolkit.
/// </summary>
internal sealed record InitializeToolStatesCommand(
    Guid SessionId,
    Guid ToolkitId) : ICommand<IReadOnlyList<ToolStateDto>>;

/// <summary>
/// Rolls dice for a specific dice tool in a session.
/// </summary>
internal sealed record RollDiceCommand(
    Guid SessionId,
    string ToolName,
    Guid? PlayerId) : ICommand<ToolStateDto>;

/// <summary>
/// Updates a counter tool value for a player in a session.
/// </summary>
internal sealed record UpdateCounterCommand(
    Guid SessionId,
    string ToolName,
    string PlayerId,
    int Change) : ICommand<ToolStateDto>;
