using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user's subscription tier changes.
/// </summary>
public sealed class UserTierChangedEvent : DomainEventBase
{
    public Guid UserId { get; }
    public UserTier OldTier { get; }
    public UserTier NewTier { get; }

    public UserTierChangedEvent(Guid userId, UserTier oldTier, UserTier newTier)
    {
        UserId = userId;
        OldTier = oldTier;
        NewTier = newTier;
    }
}
