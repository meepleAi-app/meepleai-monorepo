using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Session;

/// <summary>
/// Toggles AI agent access for a specific session participant.
/// E3-2: Only the session host can toggle agent access.
/// </summary>
public sealed record ToggleAgentAccessCommand(
    Guid SessionId,
    Guid ParticipantId,
    Guid RequestingUserId,
    bool Enabled) : IRequest;
