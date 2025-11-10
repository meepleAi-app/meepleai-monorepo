using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Seeds default AI/LLM configuration values for Production and Development environments.
    ///
    /// Configuration Keys:
    /// - AI.Model: LLM model identifier (default: deepseek/deepseek-chat-v3.1)
    /// - AI.Temperature: Response randomness, range 0.0-2.0 (default: 0.3)
    /// - AI.MaxTokens: Maximum completion length, range 1-32000 (default: 500)
    /// - AI.TimeoutSeconds: HTTP request timeout, range 1-300 (default: 60)
    ///
    /// Related:
    /// - Issue #820: feat: Create migration to seed default AI/LLM configurations
    /// - CONFIG-03: Dynamic AI/LLM Parameter Tuning
    /// - LlmService.cs: Consumes these configurations via ConfigurationService
    /// </summary>
    public partial class SeedAiLlmConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Note: Using COALESCE to handle bootstrap scenarios where no admin user exists yet
            // The migration will use the first available admin user, or fallback to system ID if none exists

            migrationBuilder.Sql(@"
                -- Ensure system user exists for CreatedByUserId
                INSERT INTO users (""Id"", ""Email"", ""DisplayName"", ""Role"", ""CreatedAt"")
                SELECT 'system', 'system@meepleai.internal', 'System', 'Admin', NOW()
                WHERE NOT EXISTS (SELECT 1 FROM users WHERE ""Id"" = 'system');

                -- Production Environment Configurations
                INSERT INTO system_configurations (
                    ""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"",
                    ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"",
                    ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId""
                ) VALUES (
                    'config-ai-model-prod',
                    'AI.Model',
                    'deepseek/deepseek-chat-v3.1',
                    'string',
                    'LLM model identifier for OpenRouter API (Production)',
                    'AI/LLM',
                    true,
                    false,
                    'Production',
                    1,
                    NOW(),
                    NOW(),
                    COALESCE(
                        (SELECT ""Id"" FROM users WHERE ""Role"" = 'Admin' ORDER BY ""CreatedAt"" LIMIT 1),
                        'system'
                    )
                ), (
                    'config-ai-temperature-prod',
                    'AI.Temperature',
                    '0.3',
                    'double',
                    'Response randomness/creativity (0.0=deterministic, 2.0=highly random) (Production)',
                    'AI/LLM',
                    true,
                    false,
                    'Production',
                    1,
                    NOW(),
                    NOW(),
                    COALESCE(
                        (SELECT ""Id"" FROM users WHERE ""Role"" = 'Admin' ORDER BY ""CreatedAt"" LIMIT 1),
                        'system'
                    )
                ), (
                    'config-ai-maxtokens-prod',
                    'AI.MaxTokens',
                    '500',
                    'int',
                    'Maximum completion length in tokens (1-32000) (Production)',
                    'AI/LLM',
                    true,
                    false,
                    'Production',
                    1,
                    NOW(),
                    NOW(),
                    COALESCE(
                        (SELECT ""Id"" FROM users WHERE ""Role"" = 'Admin' ORDER BY ""CreatedAt"" LIMIT 1),
                        'system'
                    )
                ), (
                    'config-ai-timeout-prod',
                    'AI.TimeoutSeconds',
                    '60',
                    'int',
                    'HTTP request timeout in seconds (1-300) (Production)',
                    'AI/LLM',
                    true,
                    false,
                    'Production',
                    1,
                    NOW(),
                    NOW(),
                    COALESCE(
                        (SELECT ""Id"" FROM users WHERE ""Role"" = 'Admin' ORDER BY ""CreatedAt"" LIMIT 1),
                        'system'
                    )
                ),
                -- Development Environment Configurations
                (
                    'config-ai-model-dev',
                    'AI.Model',
                    'deepseek/deepseek-chat-v3.1',
                    'string',
                    'LLM model identifier for OpenRouter API (Development)',
                    'AI/LLM',
                    true,
                    false,
                    'Development',
                    1,
                    NOW(),
                    NOW(),
                    COALESCE(
                        (SELECT ""Id"" FROM users WHERE ""Role"" = 'Admin' ORDER BY ""CreatedAt"" LIMIT 1),
                        'system'
                    )
                ), (
                    'config-ai-temperature-dev',
                    'AI.Temperature',
                    '0.3',
                    'double',
                    'Response randomness/creativity (0.0=deterministic, 2.0=highly random) (Development)',
                    'AI/LLM',
                    true,
                    false,
                    'Development',
                    1,
                    NOW(),
                    NOW(),
                    COALESCE(
                        (SELECT ""Id"" FROM users WHERE ""Role"" = 'Admin' ORDER BY ""CreatedAt"" LIMIT 1),
                        'system'
                    )
                ), (
                    'config-ai-maxtokens-dev',
                    'AI.MaxTokens',
                    '500',
                    'int',
                    'Maximum completion length in tokens (1-32000) (Development)',
                    'AI/LLM',
                    true,
                    false,
                    'Development',
                    1,
                    NOW(),
                    NOW(),
                    COALESCE(
                        (SELECT ""Id"" FROM users WHERE ""Role"" = 'Admin' ORDER BY ""CreatedAt"" LIMIT 1),
                        'system'
                    )
                ), (
                    'config-ai-timeout-dev',
                    'AI.TimeoutSeconds',
                    '60',
                    'int',
                    'HTTP request timeout in seconds (1-300) (Development)',
                    'AI/LLM',
                    true,
                    false,
                    'Development',
                    1,
                    NOW(),
                    NOW(),
                    COALESCE(
                        (SELECT ""Id"" FROM users WHERE ""Role"" = 'Admin' ORDER BY ""CreatedAt"" LIMIT 1),
                        'system'
                    )
                )
                -- Handle conflicts if configurations already exist
                ON CONFLICT (""Id"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seeded AI/LLM configurations
            migrationBuilder.Sql(@"
                DELETE FROM system_configurations
                WHERE ""Id"" IN (
                    'config-ai-model-prod',
                    'config-ai-temperature-prod',
                    'config-ai-maxtokens-prod',
                    'config-ai-timeout-prod',
                    'config-ai-model-dev',
                    'config-ai-temperature-dev',
                    'config-ai-maxtokens-dev',
                    'config-ai-timeout-dev'
                );
            ");
        }
    }
}