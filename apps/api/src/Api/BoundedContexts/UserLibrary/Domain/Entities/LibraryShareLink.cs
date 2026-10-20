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
    /// <remarks>
    /// Solo lettura per l'aggregato (#4558): la visita lo incrementa nel database con
    /// <c>ILibraryShareLinkRepository.TryRecordAccessAsync</c>, in un solo statement con il controllo di
    /// validità. Un metodo di dominio che lo incrementasse andrebbe salvato riscrivendo l'aggregato, cioè
    /// il difetto che quel metodo ha sostituito.
    /// </remarks>
    public int ViewCount { get; private set; }

    /// <summary>
    /// UTC timestamp of last access via this link.
    /// </summary>
    /// <remarks>Come <see cref="ViewCount"/>: lo scrive solo la visita, nel database.</remarks>
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
    /// Ricostituisce un link letto dal database, così com'è (#4544).
    /// </summary>
    /// <remarks>
    /// Solo per il repository. <see cref="Create"/> è la factory di un link nuovo: genera id e token,
    /// lo data ad "adesso" e rifiuta una scadenza passata, ed è giusto per un link che nasce. Usata per
    /// ricostruire una riga, rendeva illeggibile ogni link scaduto: la lettura pubblica rispondeva 400
    /// invece di 404, e la revoca falliva allo stesso modo. Un link salvato può essere scaduto o revocato:
    /// sono stati che <see cref="IsExpired"/> e <see cref="IsRevoked"/> descrivono, non errori.
    /// </remarks>
    internal static LibraryShareLink Reconstitute(
        Guid id,
        Guid userId,
        string shareToken,
        LibrarySharePrivacyLevel privacyLevel,
        bool includeNotes,
        DateTime createdAt,
        DateTime? expiresAt,
        DateTime? revokedAt,
        int viewCount,
        DateTime? lastAccessedAt) => new(id)
        {
            UserId = userId,
            ShareToken = shareToken,
            PrivacyLevel = privacyLevel,
            IncludeNotes = includeNotes,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            ViewCount = viewCount,
            LastAccessedAt = lastAccessedAt,
        };

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
