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

        // Query database metrics using direct connection
        var connection = _db.Database.GetDbConnection();

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // QUAL-01: Use parameterized queries instead of string interpolation
            long sizeResult;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT pg_database_size(current_database())";
                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                sizeResult = result != null ? Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture) : 0;
            }

            // Query connection statistics from pg_stat_database
            int activeConnections = 0;
            long transactionsCommitted = 0;
            long transactionsRolledBack = 0;

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT numbackends, xact_commit, xact_rollback
                    FROM pg_stat_database
                    WHERE datname = current_database()";

                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    activeConnections = await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false) ? 0 : reader.GetInt32(0);
                    transactionsCommitted = await reader.IsDBNullAsync(1, cancellationToken).ConfigureAwait(false) ? 0 : reader.GetInt64(1);
                    transactionsRolledBack = await reader.IsDBNullAsync(2, cancellationToken).ConfigureAwait(false) ? 0 : reader.GetInt64(2);
                }
            }

            // Query max connections setting from pg_settings
            int maxConnections = 100;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT setting FROM pg_settings WHERE name = 'max_connections'";
                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (result != null)
                {
                    _ = int.TryParse(result.ToString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out maxConnections);
                }
            }

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
                ActiveConnections: activeConnections,
                MaxConnections: maxConnections,
                TransactionsCommitted: transactionsCommitted,
                TransactionsRolledBack: transactionsRolledBack,
                MeasuredAt: DateTime.UtcNow
            );
        }
        finally
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }

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
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##} {1}", len, sizes[order]);
    }

}
