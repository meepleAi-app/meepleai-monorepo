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
            // No-op: feature flags are seeded at runtime by FeatureFlagSeeder
            // (which runs after admin user creation, avoiding FK constraint violations on fresh DBs).
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
