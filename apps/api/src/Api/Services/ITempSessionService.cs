namespace Api.Services;

/// <summary>
/// Service for managing temporary 2FA sessions (short-lived, single-use)
/// AUTH-07: Secure temp sessions between password validation and 2FA verification
/// </summary>
internal interface ITempSessionService
{
    /// <summary>
    /// Create temporary session after password validation.
    /// Returns the plaintext token and the canonical ExpiresAt stored in the
    /// row, so callers can surface the expiration to the client without
    /// recomputing it (which would drift from what was actually persisted).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <returns>Temporary session token (5-min TTL, single-use) and its UTC expiration.</returns>
    Task<(string Token, DateTime ExpiresAt)> CreateTempSessionAsync(Guid userId, string? ipAddress = null);

    /// <summary>
    /// Validate and consume temporary session token (legacy single-step path).
    /// C6: superseded by the explicit Validate / RecordFailedAttempt / Consume
    /// triple. Kept for backward compatibility while callers are migrated.
    /// </summary>
    /// <param name="token">Temporary session token</param>
    /// <returns>User ID if valid, null if invalid/expired/used</returns>
    Task<Guid?> ValidateAndConsumeTempSessionAsync(string token);

    /// <summary>
    /// C6: Validate temp session WITHOUT consuming. Returns userId iff the
    /// session exists, is not expired, is not yet IsUsed, and the failed-
    /// attempt count is under the threshold. Used by the 2FA verify path so
    /// the temp session is only marked used on successful TOTP/backup verify.
    /// </summary>
    Task<Guid?> ValidateTempSessionAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// C6: Marks temp session as used (single-use enforcement). Call only on
    /// the successful 2FA verify branch.
    /// </summary>
    Task ConsumeTempSessionAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// C6: Records a failed 2FA verification attempt. Returns true iff this
    /// attempt invalidated the temp session (i.e. crossed the configured
    /// max-failures threshold). After invalidation the session must be
    /// rejected by ValidateTempSessionAsync; the user must re-authenticate.
    /// </summary>
    Task<bool> RecordFailedAttemptAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleanup expired temporary sessions (background task)
    /// </summary>
    Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);
}
