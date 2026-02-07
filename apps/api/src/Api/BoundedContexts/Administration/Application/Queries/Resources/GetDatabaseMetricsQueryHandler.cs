using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Handler for database metrics query.
/// Uses PostgreSQL system catalogs to retrieve database statistics.
/// Issue #3695: Resources Monitoring - Database metrics
/// </summary>
internal class GetDatabaseMetricsQueryHandler : IQueryHandler<GetDatabaseMetricsQuery, DatabaseMetricsDto>
{
    private readonly MeepleAiDbContext _db;

    public GetDatabaseMetricsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<DatabaseMetricsDto> Handle(GetDatabaseMetricsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Query current database size
        var dbName = _db.Database.GetDbConnection().Database;
        var sizeQuery = $@"
            SELECT pg_database_size('{dbName}') as size_bytes
        ";

        var sizeResult = await _db.Database
            .SqlQueryRaw<long>(sizeQuery)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Query connection statistics from pg_stat_database
        var statsQuery = $@"
            SELECT
                numbackends as active_connections,
                xact_commit as transactions_committed,
                xact_rollback as transactions_rolledback
            FROM pg_stat_database
            WHERE datname = '{dbName}'
        ";

        var stats = await _db.Database
            .SqlQueryRaw<DatabaseStatsResult>(statsQuery)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Query max connections setting
        var maxConnQuery = "SHOW max_connections";
        var maxConnResult = await _db.Database
            .SqlQueryRaw<MaxConnectionsResult>(maxConnQuery)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var maxConnections = int.TryParse(maxConnResult?.MaxConnections, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var max) ? max : 100;

        // For growth trends, we would need historical data
        // For now, return zeros (can be enhanced later with stored metrics)
        var growthLast7Days = 0L;
        var growthLast30Days = 0L;
        var growthLast90Days = 0L;

        return new DatabaseMetricsDto(
            SizeBytes: sizeResult,
            SizeFormatted: FormatBytes(sizeResult),
            GrowthLast7Days: growthLast7Days,
            GrowthLast30Days: growthLast30Days,
            GrowthLast90Days: growthLast90Days,
            ActiveConnections: stats?.ActiveConnections ?? 0,
            MaxConnections: maxConnections,
            TransactionsCommitted: stats?.TransactionsCommitted ?? 0,
            TransactionsRolledBack: stats?.TransactionsRolledBack ?? 0,
            MeasuredAt: DateTime.UtcNow
        );
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
    private record DatabaseStatsResult(
        int ActiveConnections,
        long TransactionsCommitted,
        long TransactionsRolledBack
    );

    private record MaxConnectionsResult(
        string MaxConnections
    );
}
