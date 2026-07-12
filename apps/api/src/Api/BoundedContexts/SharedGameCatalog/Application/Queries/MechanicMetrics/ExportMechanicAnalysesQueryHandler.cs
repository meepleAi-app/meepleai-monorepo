using System.Globalization;
using System.Text;

using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;

internal sealed class ExportMechanicAnalysesQueryHandler
    : IQueryHandler<ExportMechanicAnalysesQuery, ExportMechanicAnalysesResult>
{
    private const int MaxRows = 10_000;

    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ExportMechanicAnalysesQueryHandler(MeepleAiDbContext db, TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ExportMechanicAnalysesResult> Handle(
        ExportMechanicAnalysesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = _db.MechanicAnalyses.AsNoTracking().AsQueryable();
        if (request.GameId is Guid gameId)
        {
            query = query.Where(a => a.SharedGameId == gameId);
        }
        if (request.ReviewerId is Guid reviewerId)
        {
            query = query.Where(a => a.ReviewedBy == reviewerId);
        }
        if (request.Status is int status)
        {
            query = query.Where(a => a.Status == status);
        }
        if (request.StartDate is DateTime start)
        {
            query = query.Where(a => a.CreatedAt >= start);
        }
        if (request.EndDate is DateTime end)
        {
            query = query.Where(a => a.CreatedAt <= end);
        }

        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(MaxRows)
            .Select(a => new MechanicRecentAnalysisRowDto(
                a.Id,
                a.SharedGameId,
                _db.SharedGames.Where(g => g.Id == a.SharedGameId).Select(g => g.Title).FirstOrDefault() ?? "—",
                a.Status,
                a.ReviewedBy,
                a.ReviewedBy == null
                    ? null
                    : _db.Users.Where(u => u.Id == a.ReviewedBy).Select(u => u.DisplayName ?? u.Email).FirstOrDefault(),
                a.CreatedAt,
                a.ReviewedAt,
                a.EstimatedCostUsd))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sb = new StringBuilder();
        // Use '\n' consistently (not AppendLine → platform-dependent \r\n) so parsers get uniform rows.
        sb.Append("Id,GameName,Status,ReviewerId,ReviewerName,CreatedAt,ReviewedAt,EstimatedCostUsd").Append('\n');
        foreach (var r in rows)
        {
            sb.Append(r.Id).Append(',')
                .Append(EscapeCsv(r.GameName)).Append(',')
                .Append(r.Status.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(r.ReviewedBy?.ToString() ?? string.Empty).Append(',')
                .Append(EscapeCsv(r.ReviewerName ?? string.Empty)).Append(',')
                .Append(r.CreatedAt.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(r.ReviewedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(r.EstimatedCostUsd.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var stamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return new ExportMechanicAnalysesResult(content, "text/csv", $"mechanic-analyses-{stamp}.csv");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
        return value;
    }
}
