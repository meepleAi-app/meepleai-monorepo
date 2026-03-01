using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Marks a player as ready for the next phase/turn.
/// Requires at least Player role.
/// Issue #4765 - Player Action Endpoints
/// </summary>
public record MarkPlayerReadyCommand(
    Guid SessionId,
    Guid ParticipantId,
    Guid RequesterId
) : IRequest<MarkPlayerReadyResult>, IRequireSessionRole
{
    public ParticipantRole MinimumRole => ParticipantRole.Player;
}

public record MarkPlayerReadyResult(
    bool IsReady,
    int ReadyCount,
    int TotalPlayers
);
