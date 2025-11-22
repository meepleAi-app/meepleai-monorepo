using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Command to verify two-factor authentication code during login.
/// Validates temporary session, verifies TOTP/backup code, and returns user ID for session creation.
/// </summary>
public sealed record Verify2FACommand : ICommand<Verify2FAResult>
{
    public string SessionToken { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// Result of 2FA verification.
/// Contains user ID if successful for subsequent session creation.
/// </summary>
public sealed record Verify2FAResult
{
    public bool Success { get; init; }
    public Guid? UserId { get; init; }
    public string? ErrorMessage { get; init; }
}
