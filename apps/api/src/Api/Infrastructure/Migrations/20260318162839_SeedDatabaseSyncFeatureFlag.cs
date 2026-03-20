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
            // No-op: feature flags are seeded at runtime by FeatureFlagSeeder
            // (which runs after admin user creation, avoiding FK constraint violations on fresh DBs).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "system_configurations" WHERE "Key" = 'Features.DatabaseSync';""");
        }
    }
}
