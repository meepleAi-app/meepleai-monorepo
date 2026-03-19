using System.Text.Json;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class SyncTableDataHandler : ICommandHandler<SyncTableDataCommand, SyncResult>
{
    private const long AdvisoryLockId = 0x4D65_6570_6C65; // "Meeple" in hex, unique lock identifier

    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;
    private readonly ILogger<SyncTableDataHandler> _logger;

    public SyncTableDataHandler(
        MeepleAiDbContext dbContext,
        IRemoteDatabaseConnector remoteConnector,
        ILogger<SyncTableDataHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _remoteConnector = remoteConnector ?? throw new ArgumentNullException(nameof(remoteConnector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SyncResult> Handle(SyncTableDataCommand command, CancellationToken cancellationToken)
    {
        var target = command.Direction == SyncDirection.LocalToStaging ? "STAGING" : "LOCAL";
        var expectedConfirmation = $"SYNC {command.TableName} TO {target}";
        if (!string.Equals(command.Confirmation, expectedConfirmation, StringComparison.Ordinal))
        {
            return new SyncResult(false, 0, 0, Guid.Empty,
                $"Confirmation mismatch. Expected: \"{expectedConfirmation}\"");
        }

        var operationId = Guid.NewGuid();
        var localConn = _dbContext.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Expected NpgsqlConnection");
        if (localConn.State != System.Data.ConnectionState.Open)
        {
            await localConn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var remoteConn = await _remoteConnector.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (remoteConn.ConfigureAwait(false))
        {
            NpgsqlConnection sourceConn;
            NpgsqlConnection targetConn;
            if (command.Direction == SyncDirection.LocalToStaging)
            {
                sourceConn = localConn;
                targetConn = remoteConn;
            }
            else
            {
                sourceConn = remoteConn;
                targetConn = localConn;
            }

            // Validate table name against information_schema (prevent SQL injection)
            var tableExists = await TableExistsAsync(sourceConn, "public", command.TableName, cancellationToken).ConfigureAwait(false);
            if (!tableExists)
                return new SyncResult(false, 0, 0, Guid.Empty, $"Table '{command.TableName}' does not exist");

            // Acquire advisory lock on target to prevent concurrent sync operations
            var lockAcquired = false;
            try
            {
                lockAcquired = await TryAcquireAdvisoryLockAsync(targetConn, cancellationToken).ConfigureAwait(false);
                if (!lockAcquired)
                {
                    return new SyncResult(false, 0, 0, operationId,
                        "Another sync operation is already in progress. Please try again later.");
                }

                // Get table metadata from source
                var pkColumns = await DataDiffEngine.GetPrimaryKeyColumnsAsync(
                    sourceConn, "public", command.TableName, cancellationToken).ConfigureAwait(false);
                if (pkColumns.Count == 0)
                {
                    return new SyncResult(false, 0, 0, operationId,
                        "Table has no primary key. Sync requires a primary key: " + command.TableName);
                }

                var allColumns = await DataDiffEngine.GetColumnsAsync(
                    sourceConn, "public", command.TableName, cancellationToken).ConfigureAwait(false);
                var safeColumns = DataDiffEngine.FilterSafeColumns(allColumns);

                // Compute diff
                var sourceHashes = await DataDiffEngine.FetchRowHashesAsync(
                    sourceConn, "public", command.TableName, pkColumns, safeColumns, cancellationToken).ConfigureAwait(false);
                var targetHashes = await DataDiffEngine.FetchRowHashesAsync(
                    targetConn, "public", command.TableName, pkColumns, safeColumns, cancellationToken).ConfigureAwait(false);
                var (_, modifiedPks, sourceOnlyPks, _) = DataDiffEngine.ComputeHashDiff(sourceHashes, targetHashes);

                if (sourceOnlyPks.Count == 0 && modifiedPks.Count == 0)
                {
                    _logger.LogInformation(
                        "No changes to sync for table {Table}. OperationId={OpId}",
                        command.TableName, operationId);
                    return new SyncResult(true, 0, 0, operationId, "No changes to sync.");
                }

                var allCols = pkColumns
                    .Concat(safeColumns.Where(c => !pkColumns.Contains(c, StringComparer.Ordinal)))
                    .ToList();
                var pkExpr = string.Join(" || '::' || ", pkColumns.Select(c => $"\"{c}\"::text"));

                // Fetch source rows for insert and update
                var pksToFetch = sourceOnlyPks.Concat(modifiedPks).ToList();
                var sourceRows = await FetchRowsAsync(
                    sourceConn, "public", command.TableName, allCols, pkExpr,
                    pksToFetch, cancellationToken).ConfigureAwait(false);

                // Execute sync in a single transaction on target
                var tx = await targetConn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await using (tx.ConfigureAwait(false))
                {
                    try
                    {
                        int inserted = 0;
                        int updated = 0;

                        foreach (var row in sourceRows)
                        {
                            var pkValue = BuildPkValue(row, pkColumns);
                            var isInsert = sourceOnlyPks.Contains(pkValue, StringComparer.Ordinal);

                            if (isInsert)
                            {
                                await InsertRowAsync(
                                    targetConn, tx, "public", command.TableName, allCols,
                                    row, cancellationToken).ConfigureAwait(false);
                                inserted++;
                            }
                            else
                            {
                                await UpdateRowAsync(
                                    targetConn, tx, "public", command.TableName, pkColumns, allCols,
                                    row, cancellationToken).ConfigureAwait(false);
                                updated++;
                            }
                        }

                        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

                        _logger.LogInformation(
                            "Synced table {Table}: {Inserted} inserted, {Updated} updated. Direction={Direction}, OperationId={OpId}",
                            command.TableName, inserted, updated, command.Direction, operationId);

                        await LogAuditAsync(command, operationId, "Success", JsonSerializer.Serialize(new
                        {
                            direction = command.Direction.ToString(),
                            inserted,
                            updated,
                            table = command.TableName
                        }), cancellationToken).ConfigureAwait(false);

                        return new SyncResult(true, inserted, updated, operationId);
                    }
                    catch (Exception ex)
                    {
                        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        _logger.LogError(ex,
                            "Failed to sync table {Table}. OperationId={OpId}",
                            command.TableName, operationId);

                        await LogAuditAsync(command, operationId, "Failure", JsonSerializer.Serialize(new
                        {
                            direction = command.Direction.ToString(),
                            error = ex.Message,
                            table = command.TableName
                        }), cancellationToken).ConfigureAwait(false);

                        return new SyncResult(false, 0, 0, operationId, ex.Message);
                    }
                }
            }
            finally
            {
                if (lockAcquired)
                {
                    await ReleaseAdvisoryLockAsync(targetConn, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task LogAuditAsync(
        SyncTableDataCommand command, Guid operationId, string result, string details,
        CancellationToken cancellationToken)
    {
        var auditLog = new AuditLog(
            id: Guid.NewGuid(),
            userId: command.AdminUserId,
            action: "DatabaseSync.SyncTableData",
            resource: command.TableName,
            result: result,
            resourceId: operationId.ToString(),
            details: details);
        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection conn, string schema, string table, CancellationToken ct)
    {
        var cmd = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema = $1 AND table_name = $2)", conn);
        await using (cmd.ConfigureAwait(false))
        {
            cmd.Parameters.AddWithValue(schema);
            cmd.Parameters.AddWithValue(table);
            return (bool)(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
        }
    }

    private static async Task<bool> TryAcquireAdvisoryLockAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var cmd = new NpgsqlCommand($"SELECT pg_try_advisory_lock({AdvisoryLockId})", conn);
        await using (cmd.ConfigureAwait(false))
        {
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result is true;
        }
    }

    private static async Task ReleaseAdvisoryLockAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var cmd = new NpgsqlCommand($"SELECT pg_advisory_unlock({AdvisoryLockId})", conn);
        await using (cmd.ConfigureAwait(false))
        {
            await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        }
    }

    private static async Task<List<object?[]>> FetchRowsAsync(
        NpgsqlConnection conn, string schema, string table,
        IReadOnlyList<string> columns, string pkExpr,
        List<string> pksToFetch, CancellationToken ct)
    {
        if (pksToFetch.Count == 0)
            return [];

        var colList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        var sql = $"SELECT {colList} FROM \"{schema}\".\"{table}\" WHERE ({pkExpr})::text = ANY($1)";
        var rows = new List<object?[]>(pksToFetch.Count);

        var cmd = new NpgsqlCommand(sql, conn);
        await using (cmd.ConfigureAwait(false))
        {
            cmd.Parameters.AddWithValue(pksToFetch.ToArray());
            var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    var values = new object?[columns.Count];
                    for (int i = 0; i < columns.Count; i++)
                    {
                        values[i] = await reader.IsDBNullAsync(i, ct).ConfigureAwait(false) ? null : reader.GetValue(i);
                    }
                    rows.Add(values);
                }
            }
        }
        return rows;
    }

    private static string BuildPkValue(object?[] row, IReadOnlyList<string> pkColumns)
    {
        if (pkColumns.Count == 1)
        {
            return row[0]?.ToString() ?? string.Empty;
        }

        return string.Join("::", pkColumns.Select((_, i) => row[i]?.ToString() ?? string.Empty));
    }

    private static async Task InsertRowAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string schema, string table,
        IReadOnlyList<string> columns, object?[] values, CancellationToken ct)
    {
        var colList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        var paramList = string.Join(", ", columns.Select((_, i) => $"${i + 1}"));
        var sql = $"INSERT INTO \"{schema}\".\"{table}\" ({colList}) VALUES ({paramList}) ON CONFLICT DO NOTHING";

        var cmd = new NpgsqlCommand(sql, conn, tx);
        await using (cmd.ConfigureAwait(false))
        {
            for (int i = 0; i < values.Length; i++)
            {
                cmd.Parameters.AddWithValue(values[i] ?? DBNull.Value);
            }
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private static async Task UpdateRowAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string schema, string table,
        IReadOnlyList<string> pkColumns, IReadOnlyList<string> allColumns,
        object?[] values, CancellationToken ct)
    {
        var columnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < allColumns.Count; i++)
        {
            columnIndex[allColumns[i]] = i;
        }

        var nonPkColumns = allColumns
            .Select((col, idx) => (col, idx))
            .Where(x => !pkColumns.Contains(x.col, StringComparer.Ordinal))
            .ToList();

        if (nonPkColumns.Count == 0)
            return; // Nothing to update beyond PK

        int paramIndex = 1;
        var setClauses = new List<string>(nonPkColumns.Count);
        var paramValues = new List<object?>();

        foreach (var (col, idx) in nonPkColumns)
        {
            setClauses.Add($"\"{col}\" = ${paramIndex}");
            paramValues.Add(values[idx]);
            paramIndex++;
        }

        var whereClauses = new List<string>(pkColumns.Count);
        foreach (var pkCol in pkColumns)
        {
            var pkIdx = columnIndex[pkCol];
            whereClauses.Add($"\"{pkCol}\" = ${paramIndex}");
            paramValues.Add(values[pkIdx]);
            paramIndex++;
        }

        var sql = $"UPDATE \"{schema}\".\"{table}\" SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}";

        var cmd = new NpgsqlCommand(sql, conn, tx);
        await using (cmd.ConfigureAwait(false))
        {
            foreach (var val in paramValues)
            {
                cmd.Parameters.AddWithValue(val ?? DBNull.Value);
            }
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
