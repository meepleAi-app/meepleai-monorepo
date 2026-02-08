namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO for audit log entries returned by the API.
/// Issue #3691: Audit Log System.
/// </summary>
internal record AuditLogDto(
    Guid Id,
    Guid? AdminUserId,
    string Action,
    string Resource,
    string? ResourceId,
    string Result,
    string? Details,
    string? IpAddress,
    DateTime CreatedAt
);

/// <summary>
/// Paginated result for audit log queries.
/// </summary>
internal record AuditLogListResult(
    IReadOnlyList<AuditLogDto> Entries,
    int TotalCount,
    int Limit,
    int Offset
);
