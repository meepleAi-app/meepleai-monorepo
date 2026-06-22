

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.Models;

/// <summary>
/// Request to enable two-factor authentication
/// </summary>
internal class TwoFactorEnableRequest
{
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Request to verify 2FA code during login
/// </summary>
internal class TwoFactorVerifyRequest
{
    public string SessionToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Request to step-up (re-verify) 2FA on the current session. SP5 Admin Security S3 — T5.
/// The session is identified by the auth cookie (resolved server-side); only the code is sent.
/// </summary>
internal class TwoFactorStepUpRequest
{
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Request to disable two-factor authentication
/// </summary>
internal class TwoFactorDisableRequest
{
    public string Password { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Admin request to disable two-factor authentication for a locked-out user.
/// I4 (auth security fixes): admins must re-authenticate by supplying their
/// own current password before this high-impact action lands. The admin
/// password is verified server-side; an admin session cookie alone is not
/// enough because the action removes a security control on another user.
/// </summary>
internal class AdminDisable2FARequest
{
    public string TargetUserId { get; set; } = string.Empty;

    /// <summary>I4: admin re-authentication password (verified by handler).</summary>
    public string AdminPassword { get; set; } = string.Empty;
}

/// <summary>
/// Response for login when 2FA is required
/// </summary>
internal class LoginWith2FaResponse
{
    public bool RequiresTwoFactor { get; set; }
    public string? SessionToken { get; set; }
    public string? Message { get; set; }
}
