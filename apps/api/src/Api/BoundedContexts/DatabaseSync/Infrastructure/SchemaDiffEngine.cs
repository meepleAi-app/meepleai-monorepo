using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Npgsql;

namespace Api.BoundedContexts.DatabaseSync.Infrastructure;

internal static class SchemaDiffEngine
{
    public static async Task<List<MigrationInfo>> ReadMigrationsAsync(NpgsqlConnection conn, CancellationToken ct = default)
    {
        var migrations = new List<MigrationInfo>();
        var cmd = new NpgsqlCommand(
            """SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" """,
            conn);
        await using (cmd.ConfigureAwait(false))
        {
            var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    migrations.Add(new MigrationInfo(
                        MigrationId: reader.GetString(0),
                        ProductVersion: reader.GetString(1),
                        AppliedOn: null
                    ));
                }
            }
        }
        return migrations;
    }

    public static SchemaDiffResult ComputeDiff(
        IReadOnlyList<MigrationInfo> localMigrations,
        IReadOnlyList<MigrationInfo> stagingMigrations)
    {
        var localIds = localMigrations.ToDictionary(m => m.MigrationId, m => m, StringComparer.Ordinal);
        var stagingIds = stagingMigrations.ToDictionary(m => m.MigrationId, m => m, StringComparer.Ordinal);

        var common = new List<MigrationInfo>();
        var localOnly = new List<MigrationInfo>();
        var stagingOnly = new List<MigrationInfo>();

        foreach (var m in localMigrations)
        {
            if (stagingIds.ContainsKey(m.MigrationId))
                common.Add(m);
            else
                localOnly.Add(m);
        }

        foreach (var m in stagingMigrations)
        {
            if (!localIds.ContainsKey(m.MigrationId))
                stagingOnly.Add(m);
        }

        return new SchemaDiffResult(common, localOnly, stagingOnly);
    }
}
