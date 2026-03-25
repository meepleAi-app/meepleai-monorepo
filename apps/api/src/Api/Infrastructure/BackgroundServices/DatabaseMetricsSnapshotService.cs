using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that takes daily database size snapshots for growth tracking.
/// Runs every 24 hours: collects database metrics from PostgreSQL system catalogs
/// and stores them in the database_metrics_snapshots table.
/// </summary>
internal sealed class DatabaseMetricsSnapshotService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DatabaseMetricsSnapshotService> _logger;

    public DatabaseMetricsSnapshotService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<DatabaseMetricsSnapshotService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DatabaseMetricsSnapshotService started. Snapshot interval: {Interval} hour(s)",
            Interval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);

#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: Generic catch prevents service from crashing the host process.
            try
            {
                await TakeSnapshotAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to create database metrics snapshot");
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("DatabaseMetricsSnapshotService stopped");
    }

    internal async Task TakeSnapshotAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);

        try
        {
            // Duplicate guard: check if a snapshot already exists for today
            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT count(*) FROM database_metrics_snapshots WHERE DATE(recorded_at) = @today::date";
                var todayParam = checkCmd.CreateParameter();
                todayParam.ParameterName = "@today";
                todayParam.Value = now.Date;
                checkCmd.Parameters.Add(todayParam);
                var count = (long)(await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false))!;

                if (count > 0)
                {
                    _logger.LogDebug("Database metrics snapshot already exists for today, skipping");
                    return;
                }
            }

            // Collect total database size
            long totalSizeBytes;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT pg_database_size(current_database())";
                var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                totalSizeBytes = result != null ? Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture) : 0;
            }

            // Count tables
            int tableCount;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*)::int FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'";
                var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                tableCount = result != null ? Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) : 0;
            }

            // Collect index size
            long indexSizeBytes;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COALESCE(SUM(pg_indexes_size(oid)), 0) FROM pg_class WHERE relkind = 'r' AND relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = 'public')";
                var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                indexSizeBytes = result != null ? Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture) : 0;
            }

            // Collect active connections
            int activeConnections;
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT numbackends FROM pg_stat_database WHERE datname = current_database()";
                var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                activeConnections = result != null ? Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) : 0;
            }

            // Insert snapshot
            using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.CommandText = @"
                    INSERT INTO database_metrics_snapshots (id, recorded_at, total_size_bytes, table_count, index_size_bytes, active_connections)
                    VALUES (@id, @recordedAt, @totalSize, @tableCount, @indexSize, @activeConns)";

                var pId = insertCmd.CreateParameter();
                pId.ParameterName = "@id";
                pId.Value = Guid.NewGuid();
                insertCmd.Parameters.Add(pId);

                var pRecordedAt = insertCmd.CreateParameter();
                pRecordedAt.ParameterName = "@recordedAt";
                pRecordedAt.Value = now;
                insertCmd.Parameters.Add(pRecordedAt);

                var pTotalSize = insertCmd.CreateParameter();
                pTotalSize.ParameterName = "@totalSize";
                pTotalSize.Value = totalSizeBytes;
                insertCmd.Parameters.Add(pTotalSize);

                var pTableCount = insertCmd.CreateParameter();
                pTableCount.ParameterName = "@tableCount";
                pTableCount.Value = tableCount;
                insertCmd.Parameters.Add(pTableCount);

                var pIndexSize = insertCmd.CreateParameter();
                pIndexSize.ParameterName = "@indexSize";
                pIndexSize.Value = indexSizeBytes;
                insertCmd.Parameters.Add(pIndexSize);

                var pActiveConns = insertCmd.CreateParameter();
                pActiveConns.ParameterName = "@activeConns";
                pActiveConns.Value = activeConnections;
                insertCmd.Parameters.Add(pActiveConns);

                await insertCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Database metrics snapshot recorded: {TotalSize} bytes, {TableCount} tables, {IndexSize} bytes indexes, {ActiveConns} connections",
                totalSizeBytes, tableCount, indexSizeBytes, activeConnections);
        }
        finally
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}
