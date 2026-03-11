using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Response record
namespace Api.BoundedContexts.Authentication.Application.Queries.Invitation;

/// <summary>
/// Query to validate an invitation token.
/// Returns whether the token is valid without exposing sensitive data like email.
/// Issue #124: User invitation system.
/// </summary>
internal record ValidateInvitationTokenQuery(string Token) : IQuery<ValidateInvitationTokenResponse>;

/// <summary>
/// Response for invitation token validation.
/// Note: Does NOT include Email for security reasons.
/// </summary>
public sealed record ValidateInvitationTokenResponse(
    bool Valid,
    string? Role,
    DateTime? ExpiresAt);
