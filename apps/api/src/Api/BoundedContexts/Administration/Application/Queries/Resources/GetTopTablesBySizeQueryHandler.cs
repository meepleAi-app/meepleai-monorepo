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

        // Three separate defects sat on top of each other here; each one hid the next, so the
        // endpoint had never worked (#3833):
        //   1. pg_stat_user_tables has no `tablename` column — that belongs to pg_tables. Here it
        //      is `relname`. Asking for the wrong one failed with 42703.
        //   2. schemaname||'.'||relname passed to the pg_*_size functions is an UNQUOTED
        //      identifier: Postgres folds it to lower case, so this database's
        //      `SystemConfiguration` schema resolves to "systemconfiguration" and the call fails
        //      with 3F000. relid is an oid and sidesteps quoting entirely.
        //   3. SqlQueryRaw<T> matches columns to property names as written. snake_case aliases
        //      never bound, so even valid SQL threw "The required column 'IndexSizeBytes' was not
        //      present". The aliases are quoted to survive Postgres' case folding.
        // The concatenation survives only for TableName, which is a display label.
        var query = $@"
            SELECT
                schemaname || '.' || relname as ""TableName"",
                pg_total_relation_size(relid) as ""TotalSizeBytes"",
                pg_relation_size(relid) as ""SizeBytes"",
                pg_indexes_size(relid) as ""IndexSizeBytes"",
                n_live_tup as ""RowCount""
            FROM pg_stat_user_tables
            ORDER BY ""TotalSizeBytes"" DESC
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
    private sealed record TableSizeResult(
        string TableName,
        long TotalSizeBytes,
        long SizeBytes,
        long IndexSizeBytes,
        long RowCount
    );
}
