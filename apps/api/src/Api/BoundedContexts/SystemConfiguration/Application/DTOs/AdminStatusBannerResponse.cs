namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// Issue #1089: Admin-facing status banner payload (full state, including inactive).
/// </summary>
public sealed record AdminStatusBannerResponse(
    string Message,
    string Severity,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt,
    DateTime UpdatedAt,
    string? UpdatedBy);
