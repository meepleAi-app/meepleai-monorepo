namespace Api.Infrastructure.Entities;

/// <summary>
/// Email verification token entity - persistence model.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
public class EmailVerificationEntity
{
    required public Guid Id { get; set; }
    required public Guid UserId { get; set; }
    required public string TokenHash { get; set; }
    required public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when this token was successfully used to verify the email.
    /// Null if token has not been used for verification.
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// Timestamp when this token was invalidated/superseded by a new token request.
    /// Null if token is still active. Distinct from VerifiedAt for audit purposes.
    /// </summary>
    public DateTime? InvalidatedAt { get; set; }

    required public DateTime CreatedAt { get; set; }

    public UserEntity User { get; set; } = null!;

    /// <summary>
    /// Returns true if this token is still active (not verified, not invalidated, not expired).
    /// </summary>
    public bool IsActive(DateTime now) =>
        VerifiedAt == null && InvalidatedAt == null && ExpiresAt > now;
}
