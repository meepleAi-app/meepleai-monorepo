using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Handler for top tables by size query.
/// Uses pg_stat_user_tables and pg_relation_size to analyze table sizes.
/// Issue #3695: Resources Monitoring - Top tables by size
/// </summary>
internal class GetTopTablesBySizeQueryHandler : IQueryHandler<GetTopTablesBySizeQuery, IReadOnlyList<TableSizeDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetTopTablesBySizeQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<TableSizeDto>> Handle(GetTopTablesBySizeQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var limit = Math.Clamp(request.Limit, 1, 100);

        // Query table sizes using PostgreSQL system catalogs
        var query = $@"
            SELECT
                schemaname || '.' || tablename as table_name,
                pg_total_relation_size(schemaname||'.'||tablename) as total_size_bytes,
                pg_relation_size(schemaname||'.'||tablename) as size_bytes,
                pg_indexes_size(schemaname||'.'||tablename) as index_size_bytes,
                n_live_tup as row_count
            FROM pg_stat_user_tables
            ORDER BY total_size_bytes DESC
            LIMIT {limit}
        ";

        var results = await _db.Database
            .SqlQueryRaw<TableSizeResult>(query)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return results.Select(r => new TableSizeDto(
            TableName: r.TableName,
            SizeBytes: r.SizeBytes,
            SizeFormatted: FormatBytes(r.SizeBytes),
            RowCount: r.RowCount,
            IndexSizeBytes: r.IndexSizeBytes,
            IndexSizeFormatted: FormatBytes(r.IndexSizeBytes),
            TotalSizeBytes: r.TotalSizeBytes,
            TotalSizeFormatted: FormatBytes(r.TotalSizeBytes)
        )).ToList();
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    // Helper record for raw SQL query results
    private record TableSizeResult(
        string TableName,
        long TotalSizeBytes,
        long SizeBytes,
        long IndexSizeBytes,
        long RowCount
    );
}
