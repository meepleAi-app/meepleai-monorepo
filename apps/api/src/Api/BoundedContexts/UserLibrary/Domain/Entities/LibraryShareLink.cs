using System.Security.Cryptography;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Aggregate root representing a shareable library link.
/// Tracks audit trail (creator, creation time, revocation) and generates secure share tokens.
/// </summary>
internal sealed class LibraryShareLink : AggregateRoot<Guid>
{
    /// <summary>
    /// User who owns the library being shared.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Secure random token for accessing the shared library (32 chars hex).
    /// </summary>
    public string ShareToken { get; private set; } = null!;

    /// <summary>
    /// Privacy level for the shared library (public or unlisted).
    /// </summary>
    public LibrarySharePrivacyLevel PrivacyLevel { get; private set; }

    /// <summary>
    /// Whether to include personal notes in the shared view.
    /// </summary>
    public bool IncludeNotes { get; private set; }

    /// <summary>
    /// UTC timestamp when the link was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// UTC timestamp when the link expires (null = never expires).
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// UTC timestamp when the link was revoked (null if still active).
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// Tracking counter for number of times the link was accessed.
    /// </summary>
    public int ViewCount { get; private set; }

    /// <summary>
    /// UTC timestamp of last access via this link.
    /// </summary>
    public DateTime? LastAccessedAt { get; private set; }

    // Constructor for domain factory method
    private LibraryShareLink(Guid id) : base(id) { }

    // EF Core constructor
#pragma warning disable CS8618
    private LibraryShareLink() : base() { }
#pragma warning restore CS8618

    /// <summary>
    /// Factory method to create a new library share link.
    /// </summary>
    /// <param name="userId">User whose library is being shared</param>
    /// <param name="privacyLevel">Privacy level (public or unlisted)</param>
    /// <param name="includeNotes">Whether to include personal notes</param>
    /// <param name="expiresAt">Optional expiration timestamp</param>
    /// <returns>New LibraryShareLink instance</returns>
    public static LibraryShareLink Create(
        Guid userId,
        LibrarySharePrivacyLevel privacyLevel,
        bool includeNotes,
        DateTime? expiresAt = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
            throw new ArgumentException("Expiration must be in the future", nameof(expiresAt));

        return new LibraryShareLink(Guid.NewGuid())
        {
            UserId = userId,
            ShareToken = GenerateSecureToken(),
            PrivacyLevel = privacyLevel,
            IncludeNotes = includeNotes,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            RevokedAt = null,
            ViewCount = 0,
            LastAccessedAt = null
        };
    }

    /// <summary>
    /// Generates a secure random token (32 hex characters).
    /// </summary>
    private static string GenerateSecureToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
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
        ViewCount++;
        LastAccessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the privacy level for this share link.
    /// </summary>
    /// <param name="newPrivacyLevel">New privacy level</param>
    public void UpdatePrivacyLevel(LibrarySharePrivacyLevel newPrivacyLevel)
    {
        if (IsRevoked)
            throw new InvalidOperationException("Cannot update privacy level for revoked share link");

        PrivacyLevel = newPrivacyLevel;
    }

    /// <summary>
    /// Updates whether notes are included in the shared view.
    /// </summary>
    /// <param name="includeNotes">Whether to include notes</param>
    public void UpdateIncludeNotes(bool includeNotes)
    {
        if (IsRevoked)
            throw new InvalidOperationException("Cannot update settings for revoked share link");

        IncludeNotes = includeNotes;
    }

    /// <summary>
    /// Updates the expiration timestamp.
    /// </summary>
    /// <param name="newExpiresAt">New expiration timestamp (null = never expires)</param>
    public void UpdateExpiration(DateTime? newExpiresAt)
    {
        if (IsRevoked)
            throw new InvalidOperationException("Cannot update expiration for revoked share link");

        if (newExpiresAt.HasValue && newExpiresAt.Value <= DateTime.UtcNow)
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
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// Gets the remaining time until expiration.
    /// </summary>
    public TimeSpan? TimeUntilExpiration =>
        ExpiresAt.HasValue && !IsExpired ? ExpiresAt.Value - DateTime.UtcNow : null;
}
