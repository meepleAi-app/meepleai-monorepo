using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a badge awarded to a user.
/// Tracks when and why the badge was earned.
/// </summary>
public sealed class UserBadge : AggregateRoot<Guid>
{
    private Guid _id;
    private Guid _userId;
    private Guid _badgeId;
    private readonly DateTime _earnedAt;
    private Guid? _triggeringShareRequestId;
    private bool _isDisplayed;
    private DateTime? _revokedAt;
    private string? _revocationReason;

    /// <summary>
    /// Gets the unique identifier of this user badge assignment.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the user who earned this badge.
    /// </summary>
    public Guid UserId => _userId;

    /// <summary>
    /// Gets the ID of the badge that was earned.
    /// </summary>
    public Guid BadgeId => _badgeId;

    /// <summary>
    /// Gets when the badge was earned.
    /// </summary>
    public DateTime EarnedAt => _earnedAt;

    /// <summary>
    /// Gets the ID of the share request that triggered earning this badge, if any.
    /// </summary>
    public Guid? TriggeringShareRequestId => _triggeringShareRequestId;

    /// <summary>
    /// Gets whether the user has chosen to display this badge publicly.
    /// </summary>
    public bool IsDisplayed => _isDisplayed;

    /// <summary>
    /// Gets when the badge was revoked, if applicable.
    /// </summary>
    public DateTime? RevokedAt => _revokedAt;

    /// <summary>
    /// Gets the reason for badge revocation, if applicable.
    /// </summary>
    public string? RevocationReason => _revocationReason;

    /// <summary>
    /// Gets whether this badge is currently active (not revoked).
    /// </summary>
    public bool IsActive => _revokedAt == null;

    /// <summary>
    /// Navigation property to the badge definition.
    /// </summary>
    public Badge? Badge { get; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable S1144 // Unused private types or members should be removed - Required for EF Core
    private UserBadge() : base()
    {
    }
#pragma warning restore S1144

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal UserBadge(
        Guid id,
        Guid userId,
        Guid badgeId,
        DateTime earnedAt,
        Guid? triggeringShareRequestId,
        bool isDisplayed,
        DateTime? revokedAt,
        string? revocationReason) : base(id)
    {
        _id = id;
        _userId = userId;
        _badgeId = badgeId;
        _earnedAt = earnedAt;
        _triggeringShareRequestId = triggeringShareRequestId;
        _isDisplayed = isDisplayed;
        _revokedAt = revokedAt;
        _revocationReason = revocationReason;
    }

    /// <summary>
    /// Awards a badge to a user.
    /// </summary>
    /// <param name="userId">The ID of the user earning the badge.</param>
    /// <param name="badgeId">The ID of the badge being earned.</param>
    /// <param name="badgeCode">The code of the badge (for the domain event).</param>
    /// <param name="triggeringShareRequestId">Optional share request that triggered the badge.</param>
    /// <returns>A new UserBadge instance.</returns>
    /// <exception cref="ArgumentException">Thrown when required parameters are invalid.</exception>
    public static UserBadge Award(
        Guid userId,
        Guid badgeId,
        string badgeCode,
        Guid? triggeringShareRequestId = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        if (badgeId == Guid.Empty)
            throw new ArgumentException("BadgeId cannot be empty", nameof(badgeId));

        if (string.IsNullOrWhiteSpace(badgeCode))
            throw new ArgumentException("BadgeCode is required", nameof(badgeCode));

        var id = Guid.NewGuid();
        var earnedAt = DateTime.UtcNow;

        var userBadge = new UserBadge(
            id,
            userId,
            badgeId,
            earnedAt,
            triggeringShareRequestId,
            isDisplayed: true,
            revokedAt: null,
            revocationReason: null);

        userBadge.AddDomainEvent(new BadgeEarnedEvent(
            id,
            userId,
            badgeId,
            badgeCode,
            earnedAt));

        return userBadge;
    }

    /// <summary>
    /// Shows this badge on the user's profile.
    /// </summary>
    public void Show()
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot show a revoked badge");

        if (_isDisplayed) return;

        _isDisplayed = true;
    }

    /// <summary>
    /// Hides this badge from the user's profile.
    /// </summary>
    public void Hide()
    {
        if (!_isDisplayed) return;

        _isDisplayed = false;
    }

    /// <summary>
    /// Revokes this badge from the user.
    /// </summary>
    /// <param name="reason">The reason for revocation.</param>
    /// <exception cref="ArgumentException">Thrown when reason is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when badge is already revoked.</exception>
    public void Revoke(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Revocation reason is required", nameof(reason));

        if (!IsActive)
            throw new InvalidOperationException("Badge is already revoked");

        _revokedAt = DateTime.UtcNow;
        _revocationReason = reason.Trim();
        _isDisplayed = false;

        AddDomainEvent(new BadgeRevokedEvent(
            _id,
            _userId,
            _badgeId,
            reason));
    }
}
