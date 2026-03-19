// OPS-02: Authentication & 2FA Metrics
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Counter for failed TOTP verification attempts.
    /// Tracks brute force attack patterns and authentication failures.
    /// </summary>
    public static readonly Counter<long> TwoFactorFailedTotpAttempts = Meter.CreateCounter<long>(
        name: "meepleai.2fa.failed_totp_attempts.total",
        unit: "attempts",
        description: "Total number of failed TOTP verification attempts");

    /// <summary>
    /// Counter for failed backup code verification attempts.
    /// Tracks potential backup code brute force attacks.
    /// </summary>
    public static readonly Counter<long> TwoFactorFailedBackupAttempts = Meter.CreateCounter<long>(
        name: "meepleai.2fa.failed_backup_attempts.total",
        unit: "attempts",
        description: "Total number of failed backup code verification attempts");

    /// <summary>
    /// Counter for blocked TOTP replay attacks.
    /// Tracks security threats from code reuse attempts.
    /// </summary>
    public static readonly Counter<long> TwoFactorReplayAttacksBlocked = Meter.CreateCounter<long>(
        name: "meepleai.2fa.replay_attacks_blocked.total",
        unit: "attacks",
        description: "Total number of TOTP replay attacks blocked");

    /// <summary>
    /// Counter for successful TOTP verifications.
    /// Baseline metric for calculating failure rates and success ratios.
    /// </summary>
    public static readonly Counter<long> TwoFactorSuccessfulTotpVerifications = Meter.CreateCounter<long>(
        name: "meepleai.2fa.successful_totp.total",
        unit: "successes",
        description: "Total number of successful TOTP verifications");

    /// <summary>
    /// Counter for successful backup code uses.
    /// Tracks backup code consumption for monitoring remaining codes per user.
    /// </summary>
    public static readonly Counter<long> TwoFactorSuccessfulBackupCodeUses = Meter.CreateCounter<long>(
        name: "meepleai.2fa.successful_backup.total",
        unit: "successes",
        description: "Total number of successful backup code uses");

    /// <summary>
    /// Counter for 2FA setup operations (generate TOTP secret + backup codes).
    /// Tracks user adoption and onboarding metrics.
    /// </summary>
    public static readonly Counter<long> TwoFactorSetupTotal = Meter.CreateCounter<long>(
        name: "meepleai.2fa.setup.total",
        unit: "setups",
        description: "Total number of 2FA setup operations");

    /// <summary>
    /// Counter for 2FA enable operations (verification + activation).
    /// Tracks successful 2FA enrollments.
    /// </summary>
    public static readonly Counter<long> TwoFactorEnableTotal = Meter.CreateCounter<long>(
        name: "meepleai.2fa.enable.total",
        unit: "enables",
        description: "Total number of 2FA enable operations");

    /// <summary>
    /// Counter for 2FA disable operations.
    /// Tracks when users disable 2FA (security posture monitoring).
    /// </summary>
    public static readonly Counter<long> TwoFactorDisableTotal = Meter.CreateCounter<long>(
        name: "meepleai.2fa.disable.total",
        unit: "disables",
        description: "Total number of 2FA disable operations");

    /// <summary>
    /// Records a 2FA verification attempt (TOTP or backup code).
    /// Tracks success/failure metrics and security events for Issue #1788 (SEC-08).
    /// </summary>
    /// <param name="verificationType">Type of verification: "totp" or "backup_code"</param>
    /// <param name="success">Whether the verification succeeded</param>
    /// <param name="userId">Optional user ID for granular tracking (should be hashed in production)</param>
    /// <param name="isReplayAttack">Whether this was a detected replay attack (TOTP only)</param>
    public static void Record2FAVerification(
        string verificationType,
        bool success,
        string? userId = null,
        bool isReplayAttack = false)
    {
        var tags = new TagList
        {
            { "verification_type", verificationType.ToLowerInvariant() },
            { "success", success }
        };

        if (!string.IsNullOrWhiteSpace(userId))
        {
            // Note: In production, consider hashing userId for GDPR compliance
            tags.Add("user_id", userId);
        }

        if (string.Equals(verificationType.ToLowerInvariant(), "totp", StringComparison.Ordinal))
        {
            if (isReplayAttack)
            {
                TwoFactorReplayAttacksBlocked.Add(1, tags);
            }
            else if (success)
            {
                TwoFactorSuccessfulTotpVerifications.Add(1, tags);
            }
            else
            {
                TwoFactorFailedTotpAttempts.Add(1, tags);
            }
        }
        else if (string.Equals(verificationType.ToLowerInvariant(), "backup_code", StringComparison.Ordinal))
        {
            if (success)
            {
                TwoFactorSuccessfulBackupCodeUses.Add(1, tags);
            }
            else
            {
                TwoFactorFailedBackupAttempts.Add(1, tags);
            }
        }
    }

    /// <summary>
    /// Records a 2FA lifecycle operation (setup, enable, disable).
    /// Tracks user adoption and security posture metrics for Issue #1788 (SEC-08).
    /// </summary>
    /// <param name="operation">Operation type: "setup", "enable", or "disable"</param>
    /// <param name="userId">Optional user ID for audit trail (should be hashed in production)</param>
    public static void Record2FALifecycle(string operation, string? userId = null)
    {
        var tags = new TagList { { "operation", operation.ToLowerInvariant() } };

        if (!string.IsNullOrWhiteSpace(userId))
        {
            tags.Add("user_id", userId);
        }

        switch (operation.ToLowerInvariant())
        {
            case "setup":
                TwoFactorSetupTotal.Add(1, tags);
                break;
            case "enable":
                TwoFactorEnableTotal.Add(1, tags);
                break;
            case "disable":
                TwoFactorDisableTotal.Add(1, tags);
                break;
        }
    }
}
