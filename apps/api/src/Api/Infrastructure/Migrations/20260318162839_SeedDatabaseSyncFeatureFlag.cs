using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDatabaseSyncFeatureFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO "system_configurations"
                    ("Id", "Key", "Value", "ValueType", "Description", "Category",
                     "IsActive", "RequiresRestart", "Environment", "Version",
                     "CreatedAt", "UpdatedAt", "CreatedByUserId")
                SELECT gen_random_uuid(), 'Features.DatabaseSync', 'false', 'bool',
                       'Enable Database Sync admin tool', 'Features', true, false, 'All', 1,
                       NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC',
                       (SELECT "Id" FROM "users" ORDER BY "CreatedAt" LIMIT 1)
                WHERE NOT EXISTS (SELECT 1 FROM "system_configurations" WHERE "Key" = 'Features.DatabaseSync')
                  AND EXISTS (SELECT 1 FROM "users");
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "system_configurations" WHERE "Key" = 'Features.DatabaseSync';""");
        }
    }
}
