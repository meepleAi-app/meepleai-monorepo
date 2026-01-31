using System.Text.Json;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to update game session state.
/// Issue #2403: GameSessionState Entity
/// </summary>
/// <param name="SessionStateId">ID of the game session state</param>
/// <param name="NewState">New state JSON</param>
public sealed record UpdateGameStateCommand(
    Guid SessionStateId,
    JsonDocument NewState) : ICommand<GameSessionStateDto>;
