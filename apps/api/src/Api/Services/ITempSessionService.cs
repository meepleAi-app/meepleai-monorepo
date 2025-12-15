namespace Api.Services;

/// <summary>
/// Service for managing temporary 2FA sessions (short-lived, single-use)
/// AUTH-07: Secure temp sessions between password validation and 2FA verification
/// </summary>
internal interface ITempSessionService
{
    /// <summary>
    /// Create temporary session after password validation
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <returns>Temporary session token (5-min TTL, single-use)</returns>
    Task<string> CreateTempSessionAsync(Guid userId, string? ipAddress = null);

    /// <summary>
    /// Validate and consume temporary session token
    /// </summary>
    /// <param name="token">Temporary session token</param>
    /// <returns>User ID if valid, null if invalid/expired/used</returns>
    Task<Guid?> ValidateAndConsumeTempSessionAsync(string token);

    /// <summary>
    /// Cleanup expired temporary sessions (background task)
    /// </summary>
    Task CleanupExpiredSessionsAsync(CancellationToken ct = default);
}
