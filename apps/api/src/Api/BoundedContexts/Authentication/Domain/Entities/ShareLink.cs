using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Aggregate root representing a shareable chat thread link.
/// Tracks audit trail (creator, creation time, revocation) and generates JWT tokens.
/// </summary>
internal sealed class ShareLink : AggregateRoot<Guid>
{
    /// <summary>
    /// Chat thread identifier that this link provides access to.
    /// </summary>
    public Guid ThreadId { get; private set; }

    /// <summary>
    /// User who created this shareable link.
    /// </summary>
    public Guid CreatorId { get; private set; }

    /// <summary>
    /// Access level granted by this link (view or comment).
    /// </summary>
    public ShareLinkRole Role { get; private set; }

    /// <summary>
    /// UTC timestamp when the link expires and becomes invalid.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// UTC timestamp when the link was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// UTC timestamp when the link was revoked (null if still active).
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// Optional label/description for the share link (for creator's reference).
    /// </summary>
    public string? Label { get; private set; }

    /// <summary>
    /// Tracking counter for number of times the link was accessed.
    /// </summary>
    public int AccessCount { get; private set; }

    /// <summary>
    /// UTC timestamp of last access via this link.
    /// </summary>
    public DateTime? LastAccessedAt { get; private set; }

    // Constructor for domain factory method
    private ShareLink(Guid id) : base(id) { }

    // EF Core constructor
    private ShareLink() : base() { }

    /// <summary>
    /// Factory method to create a new shareable chat link.
    /// </summary>
    /// <param name="threadId">Chat thread to share</param>
    /// <param name="creatorId">User creating the share link</param>
    /// <param name="role">Access level to grant</param>
    /// <param name="expiresAt">Expiration timestamp</param>
    /// <param name="label">Optional label for the link</param>
    /// <returns>New ShareLink instance</returns>
    public static ShareLink Create(
        Guid threadId,
        Guid creatorId,
        ShareLinkRole role,
        DateTime expiresAt,
        string? label = null)
    {
        if (threadId == Guid.Empty)
            throw new ArgumentException("Thread ID cannot be empty", nameof(threadId));

        if (creatorId == Guid.Empty)
            throw new ArgumentException("Creator ID cannot be empty", nameof(creatorId));

        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Expiration must be in the future", nameof(expiresAt));

        return new ShareLink(Guid.NewGuid())
        {
            ThreadId = threadId,
            CreatorId = creatorId,
            Role = role,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null,
            Label = label,
            AccessCount = 0,
            LastAccessedAt = null
        };
    }

    /// <summary>
    /// Generates a JWT token for this share link.
    /// </summary>
    /// <param name="secretKey">Secret key for JWT signing</param>
    /// <returns>ShareLinkToken instance</returns>
    public ShareLinkToken GenerateToken(string secretKey)
    {
        if (IsRevoked)
            throw new InvalidOperationException("Cannot generate token for revoked share link");

        if (IsExpired)
            throw new InvalidOperationException("Cannot generate token for expired share link");

        return ShareLinkToken.Generate(
            shareLinkId: Id,
            threadId: ThreadId,
            role: Role,
            creatorId: CreatorId,
            expiresAt: ExpiresAt,
            secretKey: secretKey
        );
    }

    /// <summary>
    /// Revokes this share link, making it immediately invalid.
    /// </summary>
    public void Revoke()
    {
        if (IsRevoked)
            throw new InvalidOperationException("Share link is already revoked");

        RevokedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records an access to this share link (for analytics/audit).
    /// </summary>
    public void RecordAccess()
    {
        AccessCount++;
        LastAccessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the expiration timestamp (e.g., to extend validity).
    /// </summary>
    /// <param name="newExpiresAt">New expiration timestamp</param>
    public void UpdateExpiration(DateTime newExpiresAt)
    {
        if (IsRevoked)
            throw new InvalidOperationException("Cannot update expiration for revoked share link");

        if (newExpiresAt <= DateTime.UtcNow)
            throw new ArgumentException("New expiration must be in the future", nameof(newExpiresAt));

        ExpiresAt = newExpiresAt;
    }

    /// <summary>
    /// Checks if the share link is currently valid (not revoked, not expired).
    /// </summary>
    public bool IsValid => !IsRevoked && !IsExpired;

    /// <summary>
    /// Checks if the share link has been revoked.
    /// </summary>
    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>
    /// Checks if the share link has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Gets the remaining time until expiration.
    /// </summary>
    public TimeSpan? TimeUntilExpiration =>
        IsExpired ? null : ExpiresAt - DateTime.UtcNow;
}
