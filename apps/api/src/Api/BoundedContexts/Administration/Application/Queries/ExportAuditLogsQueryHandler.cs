using System.Globalization;
using System.Text;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for ExportAuditLogsQuery. Exports audit logs as CSV.
/// Issue #3691: Audit Log System.
/// </summary>
internal class ExportAuditLogsQueryHandler : IQueryHandler<ExportAuditLogsQuery, ExportAuditLogsResult>
{
    private readonly MeepleAiDbContext _db;

    public ExportAuditLogsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<ExportAuditLogsResult> Handle(ExportAuditLogsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = _db.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (request.AdminUserId.HasValue)
        {
            query = query.Where(a => a.UserId == request.AdminUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(a => a.Action == request.Action);
        }

        if (!string.IsNullOrWhiteSpace(request.Resource))
        {
            query = query.Where(a => a.Resource == request.Resource);
        }

        if (!string.IsNullOrWhiteSpace(request.Result))
        {
            query = query.Where(a => a.Result == request.Result);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= request.EndDate.Value);
        }

        var entries = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(10000) // Safety limit for export
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("Id,Timestamp,AdminUserId,Action,Resource,ResourceId,Result,Details,IpAddress");

        foreach (var entry in entries)
        {
            sb.AppendLine(string.Join(",",
                entry.Id,
                entry.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                entry.UserId?.ToString() ?? "",
                EscapeCsv(entry.Action),
                EscapeCsv(entry.Resource),
                EscapeCsv(entry.ResourceId ?? ""),
                EscapeCsv(entry.Result),
                EscapeCsv(entry.Details ?? ""),
                EscapeCsv(entry.IpAddress ?? "")));
        }

        var fileName = $"audit-log-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        var content = Encoding.UTF8.GetBytes(sb.ToString());

        return new ExportAuditLogsResult(content, "text/csv", fileName);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('"', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
        return value;
    }
}
