using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class CompareTableDataHandler : IQueryHandler<CompareTableDataQuery, DataDiffResult>
{
    private const long MaxRowCount = 50_000;

    private readonly MeepleAiDbContext _dbContext;
    private readonly IRemoteDatabaseConnector _remoteConnector;

    public CompareTableDataHandler(MeepleAiDbContext dbContext, IRemoteDatabaseConnector remoteConnector)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _remoteConnector = remoteConnector ?? throw new ArgumentNullException(nameof(remoteConnector));
    }

    public async Task<DataDiffResult> Handle(CompareTableDataQuery query, CancellationToken cancellationToken)
    {
        var localConn = _dbContext.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Expected NpgsqlConnection");
        if (localConn.State != System.Data.ConnectionState.Open)
        {
            await localConn.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!await TableExistsAsync(localConn, query.TableName, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "Table does not exist on local database: " + query.TableName);
        }

        var remoteConn = await _remoteConnector.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (remoteConn.ConfigureAwait(false))
        {
            if (!await TableExistsAsync(remoteConn, query.TableName, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    "Table does not exist on staging database: " + query.TableName);
            }

            var localRowCount = await DataDiffEngine.GetEstimatedRowCountAsync(
                localConn, "public", query.TableName, cancellationToken).ConfigureAwait(false);
            var stagingRowCount = await DataDiffEngine.GetEstimatedRowCountAsync(
                remoteConn, "public", query.TableName, cancellationToken).ConfigureAwait(false);

            if (localRowCount > MaxRowCount || stagingRowCount > MaxRowCount)
            {
                throw new InvalidOperationException(
                    $"Table exceeds the {MaxRowCount:N0} row limit " +
                    $"(local: {localRowCount:N0}, staging: {stagingRowCount:N0}). " +
                    "Data comparison is not supported for large tables.");
            }

            var pkColumns = await DataDiffEngine.GetPrimaryKeyColumnsAsync(
                localConn, "public", query.TableName, cancellationToken).ConfigureAwait(false);
            if (pkColumns.Count == 0)
            {
                throw new InvalidOperationException(
                    "Table has no primary key. Data comparison requires a primary key: " + query.TableName);
            }

            var allColumns = await DataDiffEngine.GetColumnsAsync(
                localConn, "public", query.TableName, cancellationToken).ConfigureAwait(false);
            var safeColumns = DataDiffEngine.FilterSafeColumns(allColumns);

            if (safeColumns.Count == 0)
            {
                throw new InvalidOperationException(
                    "Table has no comparable columns after filtering unsafe types: " + query.TableName);
            }

            var localHashes = await DataDiffEngine.FetchRowHashesAsync(
                localConn, "public", query.TableName, pkColumns, safeColumns, cancellationToken).ConfigureAwait(false);
            var stagingHashes = await DataDiffEngine.FetchRowHashesAsync(
                remoteConn, "public", query.TableName, pkColumns, safeColumns, cancellationToken).ConfigureAwait(false);

            var (identical, modifiedPks, localOnlyPks, stagingOnlyPks) =
                DataDiffEngine.ComputeHashDiff(localHashes, stagingHashes);

            var localOnlyRows = await FetchRowsByPksAsync(
                localConn, "public", query.TableName, pkColumns, safeColumns,
                localOnlyPks, cancellationToken).ConfigureAwait(false);
            var stagingOnlyRows = await FetchRowsByPksAsync(
                remoteConn, "public", query.TableName, pkColumns, safeColumns,
                stagingOnlyPks, cancellationToken).ConfigureAwait(false);

            // Modified rows: return empty differences list per spec (full row diff is a future enhancement)
            var modifiedRows = modifiedPks
                .Select(pk => new RowDiff(
                    PrimaryKey: ParseCompositePk(pk, pkColumns),
                    Differences: Array.Empty<ColumnDiff>()))
                .ToList();

            return new DataDiffResult(
                TableName: query.TableName,
                LocalRowCount: localHashes.Count,
                StagingRowCount: stagingHashes.Count,
                IdenticalCount: identical,
                Modified: modifiedRows,
                LocalOnly: localOnlyRows,
                StagingOnly: stagingOnlyRows);
        }
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection conn, string tableName, CancellationToken ct)
    {
        var cmd = new NpgsqlCommand(
            "SELECT 1 FROM information_schema.tables WHERE table_schema = $1 AND table_name = $2 LIMIT 1",
            conn);
        await using (cmd.ConfigureAwait(false))
        {
            cmd.Parameters.AddWithValue("public");
            cmd.Parameters.AddWithValue(tableName);
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result is not null;
        }
    }

    private static async Task<List<Dictionary<string, string?>>> FetchRowsByPksAsync(
        NpgsqlConnection conn, string schema, string table,
        IReadOnlyList<string> pkColumns, IReadOnlyList<string> safeColumns,
        List<string> compositePks, CancellationToken ct)
    {
        if (compositePks.Count == 0)
            return [];

        var rows = new List<Dictionary<string, string?>>(compositePks.Count);
        var allCols = pkColumns
            .Concat(safeColumns.Where(c => !pkColumns.Contains(c, StringComparer.Ordinal)))
            .ToList();
        var colList = string.Join(", ", allCols.Select(c => $"\"{c}\""));
        var pkExpr = string.Join(" || '::' || ", pkColumns.Select(c => $"\"{c}\"::text"));

        var sql = $"SELECT {colList} FROM \"{schema}\".\"{table}\" WHERE ({pkExpr})::text = ANY($1)";
        var cmd = new NpgsqlCommand(sql, conn);
        await using (cmd.ConfigureAwait(false))
        {
            cmd.Parameters.AddWithValue(compositePks.ToArray());
            var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    var row = new Dictionary<string, string?>(StringComparer.Ordinal);
                    for (int i = 0; i < allCols.Count; i++)
                    {
                        row[allCols[i]] = await reader.IsDBNullAsync(i, ct).ConfigureAwait(false) ? null : reader.GetValue(i)?.ToString();
                    }
                    rows.Add(row);
                }
            }
        }
        return rows;
    }

    private static Dictionary<string, string?> ParseCompositePk(
        string compositePk, IReadOnlyList<string> pkColumns)
    {
        var pk = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (pkColumns.Count == 1)
        {
            pk[pkColumns[0]] = compositePk;
        }
        else
        {
            var parts = compositePk.Split("::");
            for (int i = 0; i < pkColumns.Count && i < parts.Length; i++)
            {
                pk[pkColumns[i]] = parts[i];
            }
        }
        return pk;
    }
}
