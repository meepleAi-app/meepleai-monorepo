using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to export audit log entries as CSV.
/// Issue #3691: Audit Log System.
/// </summary>
internal record ExportAuditLogsQuery(
    Guid? AdminUserId = null,
    string? Action = null,
    string? Resource = null,
    string? Result = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IQuery<ExportAuditLogsResult>;

internal record ExportAuditLogsResult(
    byte[] Content,
    string ContentType,
    string FileName
);
