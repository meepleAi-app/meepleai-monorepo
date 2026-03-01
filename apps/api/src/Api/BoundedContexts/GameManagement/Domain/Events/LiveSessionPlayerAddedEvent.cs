using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a player joins a live game session.
/// </summary>
internal sealed class LiveSessionPlayerAddedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid PlayerId { get; }
    public Guid? UserId { get; }
    public string DisplayName { get; }
    public PlayerRole Role { get; }

    public LiveSessionPlayerAddedEvent(
        Guid sessionId,
        Guid playerId,
        Guid? userId,
        string displayName,
        PlayerRole role)
    {
        SessionId = sessionId;
        PlayerId = playerId;
        UserId = userId;
        DisplayName = displayName;
        Role = role;
    }
}
