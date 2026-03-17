namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// DTO for invitation token validation response.
/// Security: only two error reasons to prevent state enumeration.
/// - "invalid" covers expired, revoked, and not-found (uniform)
/// - "already_used" allows redirect to login (acceptable disclosure)
/// Issue #124: User invitation system.
/// </summary>
internal sealed record InvitationValidationDto(
    bool IsValid,
    string? Email,
    string? DisplayName,
    string? ErrorReason);
