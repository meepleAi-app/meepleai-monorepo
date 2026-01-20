namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// LibraryShareLink entity - persistence model for library sharing.
/// Represents a shareable link to a user's game library.
/// </summary>
public class LibraryShareLinkEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to the user who owns the library being shared.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Secure random token for accessing the shared library (32 hex chars).
    /// </summary>
    public string ShareToken { get; set; } = string.Empty;

    /// <summary>
    /// Privacy level: 0 = Public, 1 = Unlisted.
    /// </summary>
    public int PrivacyLevel { get; set; }

    /// <summary>
    /// Whether to include personal notes in the shared view.
    /// </summary>
    public bool IncludeNotes { get; set; }

    /// <summary>
    /// UTC timestamp when the link was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the link expires (null = never expires).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// UTC timestamp when the link was revoked (null if still active).
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Tracking counter for number of times the link was accessed.
    /// </summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// UTC timestamp of last access via this link.
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    // Navigation properties
    public UserEntity? User { get; set; }
}
