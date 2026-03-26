using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSystemConfigurationDefaults : Migration
    {
        // Deterministic UUIDs for seed data (enables idempotent Down)
        private const string IdModelsOpenrouter = "a0000001-0001-0001-0001-000000000001";
        private const string IdModelsOllama = "a0000001-0001-0001-0001-000000000002";
        private const string IdModelsAnthropic = "a0000001-0001-0001-0001-000000000003";
        private const string IdModelsOpenai = "a0000001-0001-0001-0001-000000000004";
        private const string IdModelsAbTesting = "a0000001-0001-0001-0001-000000000005";
        private const string IdStrategiesRag = "a0000001-0001-0001-0002-000000000001";
        private const string IdStrategiesCacheTtl = "a0000001-0001-0001-0002-000000000002";
        private const string IdRerankers = "a0000001-0001-0001-0003-000000000001";
        private const string IdRateLimitsApi = "a0000001-0001-0001-0004-000000000001";
        private const string IdRateLimitsChat = "a0000001-0001-0001-0004-000000000002";
        private const string IdRateLimitsUpload = "a0000001-0001-0001-0004-000000000003";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use a PL/pgSQL block to resolve the admin user for the FK constraint.
            // If no admin user exists yet (fresh DB), we skip seeding — the app seeder will handle it.
            migrationBuilder.Sql($@"
DO $$
DECLARE
    v_user_id uuid;
    v_now timestamptz := NOW();
BEGIN
    -- Resolve first admin user (role = 'SuperAdmin' or 'Admin'), fallback to any user
    SELECT ""Id"" INTO v_user_id
    FROM users
    WHERE ""Role"" IN ('SuperAdmin', 'Admin')
    ORDER BY ""CreatedAt""
    LIMIT 1;

    IF v_user_id IS NULL THEN
        SELECT ""Id"" INTO v_user_id
        FROM users
        ORDER BY ""CreatedAt""
        LIMIT 1;
    END IF;

    -- If no users exist at all, skip seeding
    IF v_user_id IS NULL THEN
        RAISE NOTICE 'No users found — skipping system configuration seed. App seeder will handle it.';
        RETURN;
    END IF;

    -- ═══════════════════════════════════════════════════════════
    -- MODELS: OpenRouter provider models
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdModelsOpenrouter}',
        'models:openrouter',
        '[
            {{""value"":""anthropic/claude-3.5-sonnet"",""label"":""Claude 3.5 Sonnet"",""description"":""Best quality""}},
            {{""value"":""anthropic/claude-3-opus"",""label"":""Claude 3 Opus"",""description"":""Most capable""}},
            {{""value"":""anthropic/claude-3-haiku"",""label"":""Claude 3 Haiku"",""description"":""Fastest""}},
            {{""value"":""openai/gpt-4o"",""label"":""GPT-4o"",""description"":""Latest OpenAI""}},
            {{""value"":""openai/gpt-4o-mini"",""label"":""GPT-4o Mini"",""description"":""Fast & cheap""}},
            {{""value"":""meta-llama/llama-3.3-70b-instruct"",""label"":""Llama 3.3 70B"",""description"":""Paid tier""}},
            {{""value"":""meta-llama/llama-3.3-70b-instruct:free"",""label"":""Llama 3.3 70B Free"",""description"":""Rate limited""}},
            {{""value"":""google/gemini-2.0-flash-exp:free"",""label"":""Gemini 2.0 Flash Free""}},
            {{""value"":""deepseek/deepseek-chat"",""label"":""DeepSeek Chat"",""description"":""Good value""}}
        ]',
        'json',
        'OpenRouter provider models for agent playground and strategy configuration',
        'models',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

    -- ═══════════════════════════════════════════════════════════
    -- MODELS: Ollama provider models
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdModelsOllama}',
        'models:ollama',
        '[
            {{""value"":""llama3.1:8b"",""label"":""Llama 3.1 8B"",""description"":""4.9GB""}},
            {{""value"":""llama3.1:70b"",""label"":""Llama 3.1 70B"",""description"":""40GB""}},
            {{""value"":""llama3:8b"",""label"":""Llama 3 8B"",""description"":""4.7GB""}},
            {{""value"":""llama3:70b"",""label"":""Llama 3 70B"",""description"":""39GB""}},
            {{""value"":""mistral:7b"",""label"":""Mistral 7B"",""description"":""4.1GB""}},
            {{""value"":""mixtral:8x7b"",""label"":""Mixtral 8x7B"",""description"":""26GB""}},
            {{""value"":""qwen2.5:7b"",""label"":""Qwen 2.5 7B"",""description"":""4.7GB""}},
            {{""value"":""phi3:mini"",""label"":""Phi-3 Mini"",""description"":""2.3GB""}},
            {{""value"":""gemma2:9b"",""label"":""Gemma 2 9B"",""description"":""5.4GB""}}
        ]',
        'json',
        'Ollama local provider models for self-hosted inference',
        'models',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

    -- ═══════════════════════════════════════════════════════════
    -- MODELS: Anthropic direct API models
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdModelsAnthropic}',
        'models:anthropic',
        '[
            {{""value"":""claude-3-opus-20240229"",""label"":""Claude 3 Opus""}},
            {{""value"":""claude-3-5-sonnet-20241022"",""label"":""Claude 3.5 Sonnet""}},
            {{""value"":""claude-3-haiku-20240307"",""label"":""Claude 3 Haiku""}}
        ]',
        'json',
        'Anthropic direct API models',
        'models',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

    -- ═══════════════════════════════════════════════════════════
    -- MODELS: OpenAI direct API models
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdModelsOpenai}',
        'models:openai',
        '[
            {{""value"":""gpt-4o"",""label"":""GPT-4o""}},
            {{""value"":""gpt-4o-mini"",""label"":""GPT-4o Mini""}},
            {{""value"":""gpt-4-turbo"",""label"":""GPT-4 Turbo""}}
        ]',
        'json',
        'OpenAI direct API models for strategy configuration',
        'models',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

    -- ═══════════════════════════════════════════════════════════
    -- MODELS: A/B testing available models
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdModelsAbTesting}',
        'models:ab-testing',
        '[
            {{""id"":""openai/gpt-4o"",""provider"":""OpenAI"",""name"":""GPT-4o""}},
            {{""id"":""openai/gpt-4o-mini"",""provider"":""OpenAI"",""name"":""GPT-4o Mini""}},
            {{""id"":""anthropic/claude-3.5-sonnet"",""provider"":""Anthropic"",""name"":""Claude 3.5 Sonnet""}},
            {{""id"":""anthropic/claude-3-haiku"",""provider"":""Anthropic"",""name"":""Claude 3 Haiku""}},
            {{""id"":""google/gemini-2.0-flash"",""provider"":""Google"",""name"":""Gemini 2.0 Flash""}},
            {{""id"":""meta-llama/llama-3.1-70b-instruct"",""provider"":""Meta"",""name"":""Llama 3.1 70B""}},
            {{""id"":""mistralai/mistral-large-latest"",""provider"":""Mistral"",""name"":""Mistral Large""}},
            {{""id"":""qwen/qwen-2.5-72b-instruct"",""provider"":""Qwen"",""name"":""Qwen 2.5 72B""}}
        ]',
        'json',
        'Available models for A/B testing comparisons (OpenRouter format)',
        'models',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

    -- ═══════════════════════════════════════════════════════════
    -- STRATEGIES: RAG strategy options
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdStrategiesRag}',
        'strategies:rag',
        '[
            {{""value"":""SingleModel"",""label"":""SingleModel (POC)"",""description"":""RAG + single LLM call via configured provider. Default for all agents.""}},
            {{""value"":""RetrievalOnly"",""label"":""RetrievalOnly"",""description"":""Return RAG chunks only, no LLM call. Zero cost.""}},
            {{""value"":""MultiModelConsensus"",""label"":""MultiModelConsensus"",""description"":""RAG + dual-model (GPT-4 + Claude) consensus response.""}}
        ]',
        'json',
        'RAG strategy options for agent playground',
        'strategies',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

    -- ═══════════════════════════════════════════════════════════
    -- STRATEGIES: Cache TTL options
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdStrategiesCacheTtl}',
        'strategies:cache-ttl',
        '[
            {{""value"":15,""label"":""15 minutes""}},
            {{""value"":30,""label"":""30 minutes""}},
            {{""value"":60,""label"":""1 hour""}},
            {{""value"":360,""label"":""6 hours""}},
            {{""value"":1440,""label"":""24 hours""}}
        ]',
        'json',
        'Cache TTL options for RAG strategy configuration',
        'strategies',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

    -- ═══════════════════════════════════════════════════════════
    -- RERANKERS: Default reranker models
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdRerankers}',
        'rerankers:default',
        '[
            {{""value"":""cross-encoder/ms-marco-MiniLM-L-6-v2"",""label"":""MS MARCO MiniLM L-6 v2""}},
            {{""value"":""BAAI/bge-reranker-v2-m3"",""label"":""BGE Reranker v2 M3""}}
        ]',
        'json',
        'Available reranker models for RAG retrieval pipeline',
        'rerankers',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

    -- ═══════════════════════════════════════════════════════════
    -- RATE-LIMITS: API rate limits
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdRateLimitsApi}',
        'rate-limits:api',
        '{{""title"":""API Rate Limits"",""description"":""Global and per-endpoint request throttling for REST API calls."",""limits"":[{{""label"":""Global"",""value"":""1000 req/min""}},{{""label"":""Per User"",""value"":""100 req/min""}},{{""label"":""Auth Endpoints"",""value"":""20 req/min""}}]}}',
        'json',
        'API rate limit configuration — global and per-endpoint throttling',
        'rate-limits',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

    -- ═══════════════════════════════════════════════════════════
    -- RATE-LIMITS: Chat (AI) rate limits
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdRateLimitsChat}',
        'rate-limits:chat',
        '{{""title"":""Chat Rate Limits"",""description"":""Message throughput limits for AI chat sessions per tier."",""limits"":[{{""label"":""Free Tier"",""value"":""10 msg/hour""}},{{""label"":""Normal Tier"",""value"":""50 msg/hour""}},{{""label"":""Premium Tier"",""value"":""200 msg/hour""}}]}}',
        'json',
        'AI chat rate limits per user tier',
        'rate-limits',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

    -- ═══════════════════════════════════════════════════════════
    -- RATE-LIMITS: Upload rate limits
    -- ═══════════════════════════════════════════════════════════
    INSERT INTO system_configurations (""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"", ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"", ""Version"", ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId"")
    VALUES (
        '{IdRateLimitsUpload}',
        'rate-limits:upload',
        '{{""title"":""Upload Rate Limits"",""description"":""File upload frequency and bandwidth restrictions."",""limits"":[{{""label"":""Free Tier"",""value"":""5 uploads/day""}},{{""label"":""Normal Tier"",""value"":""20 uploads/day""}},{{""label"":""Premium Tier"",""value"":""100 uploads/day""}}]}}',
        'json',
        'File upload rate limits per user tier',
        'rate-limits',
        true,
        false,
        'All',
        1,
        v_now, v_now, v_user_id
    )
    ON CONFLICT (""Key"", ""Environment"") DO NOTHING;

END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
                DELETE FROM system_configurations
                WHERE ""Id"" IN (
                    '{IdModelsOpenrouter}',
                    '{IdModelsOllama}',
                    '{IdModelsAnthropic}',
                    '{IdModelsOpenai}',
                    '{IdModelsAbTesting}',
                    '{IdStrategiesRag}',
                    '{IdStrategiesCacheTtl}',
                    '{IdRerankers}',
                    '{IdRateLimitsApi}',
                    '{IdRateLimitsChat}',
                    '{IdRateLimitsUpload}'
                );
            ");
        }
    }
}
