using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a user earns a badge.
/// </summary>
internal sealed class BadgeEarnedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user badge assignment.
    /// </summary>
    public Guid UserBadgeId { get; }

    /// <summary>
    /// Gets the ID of the user who earned the badge.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the ID of the badge that was earned.
    /// </summary>
    public Guid BadgeId { get; }

    /// <summary>
    /// Gets the code of the badge that was earned.
    /// </summary>
    public string BadgeCode { get; }

    /// <summary>
    /// Gets when the badge was earned.
    /// </summary>
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
    /// <summary>
    /// Gets the ID of the user badge assignment.
    /// </summary>
    public Guid UserBadgeId { get; }

    /// <summary>
    /// Gets the ID of the user who lost the badge.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the ID of the badge that was revoked.
    /// </summary>
    public Guid BadgeId { get; }

    /// <summary>
    /// Gets the reason for revocation.
    /// </summary>
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
