using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Issue #2596: Add tier routing columns to AiModelConfigurations table.
    /// Enables database-driven LLM model selection per user tier and environment.
    /// </summary>
    public partial class AddTierRoutingToAiModelConfigurations : Migration
    {
        private static readonly string[] TierRoutingIndexColumns = ["applicable_tier", "environment_type", "is_default_for_tier"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "applicable_tier",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "environment_type",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_default_for_tier",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AiModelConfigurations_TierRouting",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                columns: TierRoutingIndexColumns);

            // Issue #2596: Seed tier routing configurations matching current appsettings.json LlmRouting
            // Tier values: Anonymous=0, User=1, Editor=2, Admin=3, Premium=4
            // Environment values: Production=0, Test=1
            SeedTierRoutingData(migrationBuilder);
        }

        private static void SeedTierRoutingData(MigrationBuilder migrationBuilder)
        {
            // JSON templates (using doubled braces for SQL string escaping)
            const string settingsJson = @"{""MaxTokens"":4096,""Temperature"":0.7,""Pricing"":{""InputPricePerMillion"":0,""OutputPricePerMillion"":0,""Currency"":""USD""}}";
            const string usageJson = @"{""TotalRequests"":0,""TotalInputTokens"":0,""TotalOutputTokens"":0,""TotalCostUsd"":0}";

            // Production tier configurations (matching current appsettings.json)
            // Anonymous (Production) - meta-llama/llama-3.3-70b-instruct:free
            migrationBuilder.Sql($"""
                INSERT INTO "SystemConfiguration"."AiModelConfigurations"
                ("Id", "ModelId", "DisplayName", "Provider", "Priority", "IsActive", "IsPrimary",
                 "applicable_tier", "environment_type", "is_default_for_tier",
                 "settings_json", "usage_json", "CreatedAt")
                VALUES
                ('{Guid.NewGuid()}', 'meta-llama/llama-3.3-70b-instruct:free', 'Llama 3.3 70B Free (Anonymous)', 'OpenRouter', 1, true, false,
                 0, 0, true,
                 '{settingsJson}',
                 '{usageJson}',
                 CURRENT_TIMESTAMP)
                ON CONFLICT ("ModelId") DO UPDATE SET
                    "applicable_tier" = EXCLUDED."applicable_tier",
                    "environment_type" = EXCLUDED."environment_type",
                    "is_default_for_tier" = EXCLUDED."is_default_for_tier";
                """);

            // User (Production) - meta-llama/llama-3.3-70b-instruct:free
            migrationBuilder.Sql($"""
                INSERT INTO "SystemConfiguration"."AiModelConfigurations"
                ("Id", "ModelId", "DisplayName", "Provider", "Priority", "IsActive", "IsPrimary",
                 "applicable_tier", "environment_type", "is_default_for_tier",
                 "settings_json", "usage_json", "CreatedAt")
                VALUES
                ('{Guid.NewGuid()}', 'meta-llama/llama-3.3-70b-instruct:free:user', 'Llama 3.3 70B Free (User)', 'OpenRouter', 1, true, false,
                 1, 0, true,
                 '{settingsJson}',
                 '{usageJson}',
                 CURRENT_TIMESTAMP)
                ON CONFLICT DO NOTHING;
                """);

            // Editor (Production) - llama3:8b (Ollama)
            migrationBuilder.Sql($"""
                INSERT INTO "SystemConfiguration"."AiModelConfigurations"
                ("Id", "ModelId", "DisplayName", "Provider", "Priority", "IsActive", "IsPrimary",
                 "applicable_tier", "environment_type", "is_default_for_tier",
                 "settings_json", "usage_json", "CreatedAt")
                VALUES
                ('{Guid.NewGuid()}', 'llama3:8b:editor', 'Llama 3 8B (Editor)', 'Ollama', 1, true, false,
                 2, 0, true,
                 '{settingsJson}',
                 '{usageJson}',
                 CURRENT_TIMESTAMP)
                ON CONFLICT DO NOTHING;
                """);

            // Admin (Production) - llama3:8b (Ollama)
            migrationBuilder.Sql($"""
                INSERT INTO "SystemConfiguration"."AiModelConfigurations"
                ("Id", "ModelId", "DisplayName", "Provider", "Priority", "IsActive", "IsPrimary",
                 "applicable_tier", "environment_type", "is_default_for_tier",
                 "settings_json", "usage_json", "CreatedAt")
                VALUES
                ('{Guid.NewGuid()}', 'llama3:8b:admin', 'Llama 3 8B (Admin)', 'Ollama', 1, true, false,
                 3, 0, true,
                 '{settingsJson}',
                 '{usageJson}',
                 CURRENT_TIMESTAMP)
                ON CONFLICT DO NOTHING;
                """);

            // Test environment configurations (same models, but can be changed independently)
            // Anonymous (Test)
            migrationBuilder.Sql($"""
                INSERT INTO "SystemConfiguration"."AiModelConfigurations"
                ("Id", "ModelId", "DisplayName", "Provider", "Priority", "IsActive", "IsPrimary",
                 "applicable_tier", "environment_type", "is_default_for_tier",
                 "settings_json", "usage_json", "CreatedAt")
                VALUES
                ('{Guid.NewGuid()}', 'meta-llama/llama-3.3-70b-instruct:free:test', 'Llama 3.3 70B Free (Anonymous Test)', 'OpenRouter', 1, true, false,
                 0, 1, true,
                 '{settingsJson}',
                 '{usageJson}',
                 CURRENT_TIMESTAMP)
                ON CONFLICT DO NOTHING;
                """);

            // User (Test)
            migrationBuilder.Sql($"""
                INSERT INTO "SystemConfiguration"."AiModelConfigurations"
                ("Id", "ModelId", "DisplayName", "Provider", "Priority", "IsActive", "IsPrimary",
                 "applicable_tier", "environment_type", "is_default_for_tier",
                 "settings_json", "usage_json", "CreatedAt")
                VALUES
                ('{Guid.NewGuid()}', 'meta-llama/llama-3.3-70b-instruct:free:user:test', 'Llama 3.3 70B Free (User Test)', 'OpenRouter', 1, true, false,
                 1, 1, true,
                 '{settingsJson}',
                 '{usageJson}',
                 CURRENT_TIMESTAMP)
                ON CONFLICT DO NOTHING;
                """);

            // Editor (Test)
            migrationBuilder.Sql($"""
                INSERT INTO "SystemConfiguration"."AiModelConfigurations"
                ("Id", "ModelId", "DisplayName", "Provider", "Priority", "IsActive", "IsPrimary",
                 "applicable_tier", "environment_type", "is_default_for_tier",
                 "settings_json", "usage_json", "CreatedAt")
                VALUES
                ('{Guid.NewGuid()}', 'llama3:8b:editor:test', 'Llama 3 8B (Editor Test)', 'Ollama', 1, true, false,
                 2, 1, true,
                 '{settingsJson}',
                 '{usageJson}',
                 CURRENT_TIMESTAMP)
                ON CONFLICT DO NOTHING;
                """);

            // Admin (Test)
            migrationBuilder.Sql($"""
                INSERT INTO "SystemConfiguration"."AiModelConfigurations"
                ("Id", "ModelId", "DisplayName", "Provider", "Priority", "IsActive", "IsPrimary",
                 "applicable_tier", "environment_type", "is_default_for_tier",
                 "settings_json", "usage_json", "CreatedAt")
                VALUES
                ('{Guid.NewGuid()}', 'llama3:8b:admin:test', 'Llama 3 8B (Admin Test)', 'Ollama', 1, true, false,
                 3, 1, true,
                 '{settingsJson}',
                 '{usageJson}',
                 CURRENT_TIMESTAMP)
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiModelConfigurations_TierRouting",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "applicable_tier",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "environment_type",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "is_default_for_tier",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");
        }
    }
}
