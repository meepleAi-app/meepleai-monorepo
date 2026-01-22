namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// EF Core persistence entity for UserBadge aggregate root.
/// Tracks badges awarded to users.
/// ISSUE-2731
/// </summary>
public class UserBadgeEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BadgeId { get; set; }
    public DateTime EarnedAt { get; set; }
    public Guid? TriggeringShareRequestId { get; set; }
    public bool IsDisplayed { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }

    // Navigation properties
    public BadgeEntity? Badge { get; set; }
}
