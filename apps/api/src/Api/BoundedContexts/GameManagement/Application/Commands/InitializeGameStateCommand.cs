using System.Text.Json;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to initialize game session state from template defaults.
/// Issue #2403: GameSessionState Entity
/// </summary>
/// <param name="GameSessionId">ID of the game session</param>
/// <param name="TemplateId">ID of the GameStateTemplate to use</param>
/// <param name="InitialState">Initial state JSON (optional, defaults from template if null)</param>
public sealed record InitializeGameStateCommand(
    Guid GameSessionId,
    Guid TemplateId,
    JsonDocument? InitialState) : ICommand<GameSessionStateDto>;
