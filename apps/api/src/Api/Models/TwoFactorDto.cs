

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.Models;

/// <summary>
/// Request to enable two-factor authentication
/// </summary>
public class TwoFactorEnableRequest
{
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Request to verify 2FA code during login
/// </summary>
public class TwoFactorVerifyRequest
{
    public string SessionToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Request to disable two-factor authentication
/// </summary>
public class TwoFactorDisableRequest
{
    public string Password { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Admin request to disable two-factor authentication for a locked-out user
/// </summary>
public class AdminDisable2FARequest
{
    public string TargetUserId { get; set; } = string.Empty;
}

/// <summary>
/// Response for login when 2FA is required
/// </summary>
public class LoginWith2FaResponse
{
    public bool RequiresTwoFactor { get; set; }
    public string? SessionToken { get; set; }
    public string? Message { get; set; }
}
