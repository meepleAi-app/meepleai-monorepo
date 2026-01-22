using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a badge is earned by a user.
/// </summary>
internal sealed class BadgeEarnedEvent : DomainEventBase
{
    public Guid UserBadgeId { get; }
    public Guid UserId { get; }
    public Guid BadgeId { get; }
    public string BadgeCode { get; }
    public DateTime EarnedAt { get; }

    public BadgeEarnedEvent(
        Guid userBadgeId,
        Guid userId,
        Guid badgeId,
        string badgeCode,
        DateTime earnedAt)
    {
        UserBadgeId = userBadgeId;
        UserId = userId;
        BadgeId = badgeId;
        BadgeCode = badgeCode;
        EarnedAt = earnedAt;
    }
}

/// <summary>
/// Domain event raised when a badge is revoked from a user.
/// </summary>
internal sealed class BadgeRevokedEvent : DomainEventBase
{
    public Guid UserBadgeId { get; }
    public Guid UserId { get; }
    public Guid BadgeId { get; }
    public string Reason { get; }

    public BadgeRevokedEvent(
        Guid userBadgeId,
        Guid userId,
        Guid badgeId,
        string reason)
    {
        UserBadgeId = userBadgeId;
        UserId = userId;
        BadgeId = badgeId;
        Reason = reason;
    }
}
