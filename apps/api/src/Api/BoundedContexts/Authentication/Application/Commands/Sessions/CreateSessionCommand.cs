using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to create a new session for an authenticated user.
/// Used after OAuth callback or 2FA verification.
/// </summary>
internal record CreateSessionCommand(
    Guid UserId,
    string? IpAddress = null,
    string? UserAgent = null,
    Guid? ImpersonatedByUserId = null,    // SP5 S2: actor (admin) when this session is an impersonation
    DateTime? ImpersonatedUntil = null,   // SP5 S2: auto-expiry timestamp for impersonate sessions
    DateTime? LastTotpVerifiedAt = null   // SP5 S3: pre-seeded TOTP recency (impersonate inherits actor's value)
) : ICommand<CreateSessionResponse>;

internal record CreateSessionResponse(
    UserDto User,
    string SessionToken,
    DateTime ExpiresAt,
    Guid SessionId = default   // SP5 S2: exposed so callers (e.g. impersonation) can reference the row
);
