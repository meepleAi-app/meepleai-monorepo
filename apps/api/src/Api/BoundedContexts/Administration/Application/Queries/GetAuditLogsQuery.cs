using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get paginated audit log entries with optional filters.
/// Issue #3691: Audit Log System.
/// </summary>
internal record GetAuditLogsQuery(
    int Limit = 50,
    int Offset = 0,
    Guid? AdminUserId = null,
    string? Action = null,
    string? Resource = null,
    string? Result = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IQuery<AuditLogListResult>;
