using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Command to generate TOTP setup for two-factor authentication.
/// Creates secret, QR code URL, and backup codes for user enrollment.
/// </summary>
public sealed record GenerateTotpSetupCommand : ICommand<GenerateTotpSetupResult>
{
    public Guid UserId { get; init; }
    public string UserEmail { get; init; } = string.Empty;
}

/// <summary>
/// Result of TOTP setup generation.
/// Contains secret key, QR code URL, and backup codes for user.
/// </summary>
public sealed record GenerateTotpSetupResult
{
    public string Secret { get; init; } = string.Empty;
    public string QrCodeUrl { get; init; } = string.Empty;
    public List<string> BackupCodes { get; init; } = new();
}
