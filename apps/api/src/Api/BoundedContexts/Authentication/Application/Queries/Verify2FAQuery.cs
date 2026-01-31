using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to verify a TOTP code or backup code for two-factor authentication.
/// DDD CQRS: Query with business logic delegated to domain.
/// </summary>
internal sealed record Verify2FAQuery(
    string Email,
    string Code,
    bool IsBackupCode = false
) : IQuery<Verify2FAResult>;

/// <summary>
/// Result of 2FA verification.
/// </summary>
internal sealed record Verify2FAResult(
    bool IsValid,
    string? ErrorMessage = null
);
