using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedGameNightV2FeatureFlags : Migration
    {
        private static readonly (string Key, bool IsEnabled, string Description)[] Flags =
        [
            ("Features:SetupWizard.Enabled", true, "Enable Setup Wizard for live sessions"),
            ("Features:SetupWizard.BggFallback", false, "Enable BGG data fallback in setup checklist cascade"),
            ("Features:Arbitro.StructuredDisputes", true, "Enable v2 structured dispute system"),
            ("Features:Arbitro.DemocraticOverride", true, "Enable democratic override voting on dispute verdicts"),
            ("Features:AgentMemory.Enabled", true, "Enable persistent agent memory system"),
            ("Features:AgentMemory.GuestClaim", true, "Enable guest account claiming for player history"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = "NOW() AT TIME ZONE 'UTC'";

            foreach (var (key, isEnabled, description) in Flags)
            {
                var value = isEnabled ? "true" : "false";
                migrationBuilder.Sql($"""
                    INSERT INTO "system_configurations"
                        ("Id", "Key", "Value", "ValueType", "Description", "Category",
                         "IsActive", "RequiresRestart", "Environment", "Version",
                         "CreatedAt", "UpdatedAt", "CreatedByUserId")
                    SELECT
                        gen_random_uuid(),
                        '{key}',
                        '{value}',
                        'bool',
                        '{description}',
                        'Features',
                        true,
                        false,
                        'All',
                        1,
                        {now},
                        {now},
                        COALESCE(
                            (SELECT "Id" FROM "users" WHERE "Role" = 'Admin' ORDER BY "CreatedAt" LIMIT 1),
                            '00000000-0000-0000-0000-000000000000'::uuid
                        )
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "system_configurations"
                        WHERE "Key" = '{key}' AND "Environment" = 'All'
                    );
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (key, _, _) in Flags)
            {
                migrationBuilder.Sql($"""
                    DELETE FROM "system_configurations"
                    WHERE "Key" = '{key}' AND "Environment" = 'All';
                    """);
            }
        }
    }
}
