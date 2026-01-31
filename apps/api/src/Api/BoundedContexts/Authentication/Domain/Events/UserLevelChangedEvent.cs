using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user's level changes.
/// Issue #3141: Track user level modifications for audit trail.
/// </summary>
internal sealed class UserLevelChangedEvent : DomainEventBase
{
    public Guid UserId { get; }
    public int OldLevel { get; }
    public int NewLevel { get; }

    public UserLevelChangedEvent(Guid userId, int oldLevel, int newLevel)
    {
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
    }
}
