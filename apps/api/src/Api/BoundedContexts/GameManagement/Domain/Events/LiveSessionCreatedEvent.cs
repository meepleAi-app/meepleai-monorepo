using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a new live game session is created.
/// </summary>
internal sealed class LiveSessionCreatedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid CreatedByUserId { get; }
    public string GameName { get; }
    public Guid? GameId { get; }

    public LiveSessionCreatedEvent(Guid sessionId, Guid createdByUserId, string gameName, Guid? gameId)
    {
        SessionId = sessionId;
        CreatedByUserId = createdByUserId;
        GameName = gameName;
        GameId = gameId;
    }
}
