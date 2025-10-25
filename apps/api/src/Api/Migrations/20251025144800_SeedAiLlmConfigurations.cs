using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <summary>
    /// CONFIG-03: Seed AI/LLM configuration parameters into system_configurations table.
    /// Provides database-driven configuration for model selection, temperature, max_tokens, and timeout.
    /// Fallback chain: Database → appsettings.json → hardcoded defaults in LlmService.
    /// </summary>
    public partial class SeedAiLlmConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CONFIG-03: Insert AI/LLM configuration parameters
            // These configurations control the LLM behavior for all chat completions
            migrationBuilder.Sql(@"
                INSERT INTO system_configurations (
                    ""Id"",
                    ""Key"",
                    ""Value"",
                    ""ValueType"",
                    ""Description"",
                    ""Category"",
                    ""Environment"",
                    ""IsActive"",
                    ""CreatedAt"",
                    ""UpdatedAt"",
                    ""CreatedBy"",
                    ""UpdatedBy""
                ) VALUES
                -- Production environment configurations
                (
                    'config-ai-model-prod',
                    'AI.Model',
                    '""deepseek/deepseek-chat-v3.1""',
                    'String',
                    'LLM model to use for chat completions (OpenRouter model identifier)',
                    'AI/LLM',
                    'Production',
                    true,
                    NOW(),
                    NOW(),
                    'system',
                    'system'
                ),
                (
                    'config-ai-temperature-prod',
                    'AI.Temperature',
                    '0.3',
                    'Double',
                    'Temperature for LLM responses (0.0-2.0, lower = more deterministic)',
                    'AI/LLM',
                    'Production',
                    true,
                    NOW(),
                    NOW(),
                    'system',
                    'system'
                ),
                (
                    'config-ai-maxtokens-prod',
                    'AI.MaxTokens',
                    '500',
                    'Integer',
                    'Maximum tokens for LLM completion (controls response length)',
                    'AI/LLM',
                    'Production',
                    true,
                    NOW(),
                    NOW(),
                    'system',
                    'system'
                ),
                (
                    'config-ai-timeout-prod',
                    'AI.TimeoutSeconds',
                    '60',
                    'Integer',
                    'HTTP timeout for LLM API requests in seconds',
                    'AI/LLM',
                    'Production',
                    true,
                    NOW(),
                    NOW(),
                    'system',
                    'system'
                ),
                -- Development environment configurations
                (
                    'config-ai-model-dev',
                    'AI.Model',
                    '""deepseek/deepseek-chat-v3.1""',
                    'String',
                    'LLM model to use for chat completions (Development)',
                    'AI/LLM',
                    'Development',
                    true,
                    NOW(),
                    NOW(),
                    'system',
                    'system'
                ),
                (
                    'config-ai-temperature-dev',
                    'AI.Temperature',
                    '0.3',
                    'Double',
                    'Temperature for LLM responses (Development)',
                    'AI/LLM',
                    'Development',
                    true,
                    NOW(),
                    NOW(),
                    'system',
                    'system'
                ),
                (
                    'config-ai-maxtokens-dev',
                    'AI.MaxTokens',
                    '500',
                    'Integer',
                    'Maximum tokens for LLM completion (Development)',
                    'AI/LLM',
                    'Development',
                    true,
                    NOW(),
                    NOW(),
                    'system',
                    'system'
                ),
                (
                    'config-ai-timeout-dev',
                    'AI.TimeoutSeconds',
                    '60',
                    'Integer',
                    'HTTP timeout for LLM API requests in seconds (Development)',
                    'AI/LLM',
                    'Development',
                    true,
                    NOW(),
                    NOW(),
                    'system',
                    'system'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // CONFIG-03: Remove AI/LLM configuration parameters
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
