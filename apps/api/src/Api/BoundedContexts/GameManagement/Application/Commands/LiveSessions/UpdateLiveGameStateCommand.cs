using System.Text.Json;

using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Updates the free-form live game-state (#3025 L1). Host/participant only.
/// <para>
/// <see cref="State"/> is a <see cref="JsonElement"/> — it binds cleanly from the request
/// body and carries no <see cref="JsonDocument"/> disposal concern. The handler parses it
/// into an owned <see cref="JsonDocument"/> that ownership is transferred to the aggregate.
/// </para>
/// </summary>
internal record UpdateLiveGameStateCommand(
    Guid SessionId,
    Guid RequestedByUserId,
    JsonElement State
) : ICommand;
