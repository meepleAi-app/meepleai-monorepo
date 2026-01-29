namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO for role change history entry (Issue #2890).
/// </summary>
public record RoleChangeHistoryDto(
    DateTime ChangedAt,
    string OldRole,
    string NewRole,
    Guid ChangedBy,
    string ChangedByDisplayName,
    string? IpAddress
);
