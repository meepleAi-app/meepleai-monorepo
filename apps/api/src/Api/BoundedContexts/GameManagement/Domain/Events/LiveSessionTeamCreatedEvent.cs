using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a team is created in a live game session.
/// </summary>
internal sealed class LiveSessionTeamCreatedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid TeamId { get; }
    public string TeamName { get; }

    public LiveSessionTeamCreatedEvent(Guid sessionId, Guid teamId, string teamName)
    {
        SessionId = sessionId;
        TeamId = teamId;
        TeamName = teamName;
    }
}
