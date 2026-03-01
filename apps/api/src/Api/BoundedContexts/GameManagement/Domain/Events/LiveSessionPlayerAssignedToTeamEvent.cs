using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a player is assigned to a team in a live game session.
/// </summary>
internal sealed class LiveSessionPlayerAssignedToTeamEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid PlayerId { get; }
    public Guid TeamId { get; }

    public LiveSessionPlayerAssignedToTeamEvent(Guid sessionId, Guid playerId, Guid teamId)
    {
        SessionId = sessionId;
        PlayerId = playerId;
        TeamId = teamId;
    }
}
