namespace Api.Services;

/// <summary>
/// Service for TOTP-based two-factor authentication
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Generate TOTP secret, QR code URL, and backup codes for 2FA enrollment
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="userEmail">User email for QR code</param>
    /// <returns>Setup response with secret, QR URL, and backup codes</returns>
    Task<TotpSetupResponse> GenerateSetupAsync(string userId, string userEmail);

    /// <summary>
    /// Enable two-factor authentication after verifying TOTP code
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="totpCode">6-digit TOTP code from authenticator app</param>
    /// <returns>True if code valid and 2FA enabled, false otherwise</returns>
    Task<bool> EnableTwoFactorAsync(string userId, string totpCode);

    /// <summary>
    /// Verify TOTP code during login
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="code">6-digit TOTP code</param>
    /// <returns>True if code valid, false otherwise</returns>
    Task<bool> VerifyCodeAsync(string userId, string code);

    /// <summary>
    /// Verify backup code and mark as used
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="backupCode">8-character backup code</param>
    /// <returns>True if code valid and unused, false otherwise</returns>
    Task<bool> VerifyBackupCodeAsync(string userId, string backupCode);

    /// <summary>
    /// Disable two-factor authentication with password and code verification
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="password">User password</param>
    /// <param name="totpOrBackupCode">TOTP code or backup code</param>
    Task DisableTwoFactorAsync(string userId, string password, string totpOrBackupCode);

    /// <summary>
    /// Get two-factor authentication status for user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>2FA status including enabled state and backup codes count</returns>
    Task<TwoFactorStatusResponse> GetTwoFactorStatusAsync(string userId);
}

/// <summary>
/// Response for TOTP setup containing secret and backup codes
/// </summary>
public class TotpSetupResponse
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public List<string> BackupCodes { get; set; } = new();
}

/// <summary>
/// Response for 2FA status
/// </summary>
public class TwoFactorStatusResponse
{
    public bool IsEnabled { get; set; }
    public DateTime? EnabledAt { get; set; }
    public int UnusedBackupCodesCount { get; set; }
}
