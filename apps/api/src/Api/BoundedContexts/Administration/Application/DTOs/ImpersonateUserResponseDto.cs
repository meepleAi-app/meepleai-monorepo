namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Response for impersonate user action (Issue #2890).
/// </summary>
public record ImpersonateUserResponseDto(
    string SessionToken,
    Guid ImpersonatedUserId,
    DateTime ExpiresAt
);
