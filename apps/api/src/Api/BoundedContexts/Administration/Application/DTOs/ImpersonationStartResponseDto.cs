namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Response for <c>ImpersonationStartCommand</c> (SP5 Admin Security S2 — T3).
/// Successor to the legacy <c>ImpersonateUserResponseDto</c> (issues #2890 / #3349).
/// </summary>
public record ImpersonationStartResponseDto(
    Guid SessionId,
    string SessionToken,
    Guid ImpersonatedUserId,
    DateTime ImpersonatedUntil,
    DateTime ExpiresAt
);
