using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Infrastructure;

internal static class DataDiffEngine
{
    private static readonly HashSet<string> UnsafeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bytea", "vector", "json", "jsonb", "xml"
    };

    public static List<string> FilterSafeColumns(IReadOnlyList<(string name, string type)> columns)
    {
        return columns
            .Where(c => !UnsafeTypes.Contains(c.type))
            .Select(c => c.name)
            .ToList();
    }

    public static async Task<List<(string name, string type)>> GetColumnsAsync(
        NpgsqlConnection conn, string schema, string table, CancellationToken ct = default)
    {
        var columns = new List<(string, string)>();
        var cmd = new NpgsqlCommand(
            "SELECT column_name, udt_name FROM information_schema.columns WHERE table_schema = $1 AND table_name = $2 ORDER BY ordinal_position",
            conn);
        await using (cmd.ConfigureAwait(false))
        {
            cmd.Parameters.AddWithValue(schema);
            cmd.Parameters.AddWithValue(table);
            var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    columns.Add((reader.GetString(0), reader.GetString(1)));
            }
        }
        return columns;
    }

    public static async Task<List<string>> GetPrimaryKeyColumnsAsync(
        NpgsqlConnection conn, string schema, string table, CancellationToken ct = default)
    {
        var pkCols = new List<string>();
        var cmd = new NpgsqlCommand("""
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
            WHERE tc.table_schema = $1 AND tc.table_name = $2 AND tc.constraint_type = 'PRIMARY KEY'
            ORDER BY kcu.ordinal_position
        """, conn);
        await using (cmd.ConfigureAwait(false))
        {
            cmd.Parameters.AddWithValue(schema);
            cmd.Parameters.AddWithValue(table);
            var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    pkCols.Add(reader.GetString(0));
            }
        }
        return pkCols;
    }

    public static async Task<Dictionary<string, string>> FetchRowHashesAsync(
        NpgsqlConnection conn, string schema, string table,
        IReadOnlyList<string> pkColumns, IReadOnlyList<string> safeColumns,
        CancellationToken ct = default)
    {
        var pkExpr = string.Join(" || '::' || ", pkColumns.Select(c => $"\"{c}\"::text"));
        var colList = string.Join(", ", safeColumns.Select(c => $"\"{c}\""));
        var sql = $"SELECT {pkExpr} as pk, md5(ROW({colList})::text) as row_hash FROM \"{schema}\".\"{table}\"";
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var cmd = new NpgsqlCommand(sql, conn);
        await using (cmd.ConfigureAwait(false))
        {
            var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    hashes[reader.GetString(0)] = reader.GetString(1);
            }
        }
        return hashes;
    }

    public static (int identical, List<string> modified, List<string> localOnly, List<string> stagingOnly)
        ComputeHashDiff(Dictionary<string, string> localHashes, Dictionary<string, string> stagingHashes)
    {
        int identical = 0;
        var modified = new List<string>();
        var localOnly = new List<string>();
        var stagingOnly = new List<string>();

        foreach (var (pk, hash) in localHashes)
        {
            if (stagingHashes.TryGetValue(pk, out var stagingHash))
            {
                if (string.Equals(hash, stagingHash, StringComparison.Ordinal)) identical++;
                else modified.Add(pk);
            }
            else localOnly.Add(pk);
        }

        foreach (var pk in stagingHashes.Keys)
        {
            if (!localHashes.ContainsKey(pk))
                stagingOnly.Add(pk);
        }

        return (identical, modified, localOnly, stagingOnly);
    }

    public static async Task<long> GetEstimatedRowCountAsync(
        NpgsqlConnection conn, string schema, string table, CancellationToken ct = default)
    {
        var cmd = new NpgsqlCommand(
            "SELECT COALESCE(n_live_tup, 0) FROM pg_stat_user_tables WHERE schemaname = $1 AND relname = $2",
            conn);
        await using (cmd.ConfigureAwait(false))
        {
            cmd.Parameters.AddWithValue(schema);
            cmd.Parameters.AddWithValue(table);
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result is long count ? count : 0;
        }
    }
}
