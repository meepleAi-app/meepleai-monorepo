using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CONFIG-05: Seed 8 initial feature flags for Production and Development environments
            // Format: Features.{FeatureName} for global, Features.{FeatureName}.{Role} for role-specific

            var featureFlags = new[]
            {
                // Features enabled by default for all users
                new { Key = "Features.StreamingResponses", Value = "true", Description = "Enable SSE streaming for chat responses" },
                new { Key = "Features.SetupGuideGeneration", Value = "true", Description = "Enable AI-powered setup guide generation" },
                new { Key = "Features.PdfUpload", Value = "true", Description = "Enable PDF rulebook upload functionality" },
                new { Key = "Features.ChatExport", Value = "true", Description = "Enable chat export (markdown, PDF, text)" },
                new { Key = "Features.MessageEditDelete", Value = "true", Description = "Enable message edit/delete in chat" },
                new { Key = "Features.N8nIntegration", Value = "true", Description = "Enable n8n webhook-based agents" },

                // Features restricted to Admin role only
                new { Key = "Features.RagEvaluation.Admin", Value = "false", Description = "Enable RAG evaluation endpoints (Admin only)" },
                new { Key = "Features.AdvancedAdmin.Admin", Value = "false", Description = "Enable advanced admin features (Admin only)" }
            };

            foreach (var env in new[] { "Production", "Development" })
            {
                foreach (var flag in featureFlags)
                {
                    migrationBuilder.Sql($@"
                        INSERT INTO system_configurations (
                            ""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"",
                            ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"",
                            ""CreatedAt"", ""UpdatedAt"", ""CreatedBy"", ""UpdatedBy""
                        ) VALUES (
                            gen_random_uuid(),
                            '{flag.Key}',
                            '{flag.Value}',
                            'Boolean',
                            '{flag.Description}',
                            'FeatureFlags',
                            true,
                            false,
                            '{env}',
                            NOW(),
                            NOW(),
                            'system',
                            'system'
                        );
                    ");
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove all feature flag configurations
            migrationBuilder.Sql(@"
                DELETE FROM system_configurations
                WHERE ""Category"" = 'FeatureFlags';
            ");
        }
    }
}
