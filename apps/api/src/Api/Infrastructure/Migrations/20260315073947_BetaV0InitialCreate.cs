using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BetaV0InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "knowledge_base");

            migrationBuilder.EnsureSchema(
                name: "SystemConfiguration");

            migrationBuilder.EnsureSchema(
                name: "administration");

            migrationBuilder.EnsureSchema(
                name: "session_tracking");

            migrationBuilder.EnsureSchema(
                name: "entity_relationships");

            migrationBuilder.EnsureSchema(
                name: "game_toolbox");

            migrationBuilder.EnsureSchema(
                name: "game_toolkit");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "ab_test_sessions",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    query = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    knowledge_base_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ab_test_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "achievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Rarity = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Threshold = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_achievements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "admin_rag_strategies",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    steps_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_rag_strategies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admin_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    template = table.Column<int>(type: "integer", nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    schedule_expression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email_recipients = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_definitions",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    max_tokens = table.Column<int>(type: "integer", nullable: false),
                    temperature = table.Column<float>(type: "real", nullable: false),
                    chat_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "auto"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    kb_card_ids = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    prompts = table.Column<string>(type: "jsonb", nullable: false),
                    strategy = table.Column<string>(type: "jsonb", nullable: false),
                    tools = table.Column<string>(type: "jsonb", nullable: false),
                    type_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", maxLength: 128, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_feedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agent_typologies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    base_prompt = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    default_strategy_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_typologies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_request_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    Endpoint = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Query = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ResponseSnippet = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FinishReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RagConfidence = table.Column<double>(type: "double precision", nullable: true),
                    LlmConfidence = table.Column<double>(type: "double precision", nullable: true),
                    CitationQuality = table.Column<double>(type: "double precision", nullable: true),
                    OverallConfidence = table.Column<double>(type: "double precision", nullable: true),
                    IsLowQuality = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_request_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiModelConfigurations",
                schema: "SystemConfiguration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    applicable_tier = table.Column<int>(type: "integer", nullable: true),
                    environment_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_default_for_tier = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    settings_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    PricingJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    usage_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "alert_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    config_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    config_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_encrypted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alert_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    threshold = table.Column<double>(type: "double precision", nullable: false),
                    threshold_unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    triggered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    channel_sent = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "analysis_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsReviewed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsExported = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_feedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Resource = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Details = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "badges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    icon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    requirement = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_badges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "batch_jobs",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Parameters = table.Column<string>(type: "jsonb", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    ResultData = table.Column<string>(type: "jsonb", nullable: true),
                    ResultSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OutputFileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorStack = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BggImportQueue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobType = table.Column<int>(type: "integer", nullable: false),
                    BggId = table.Column<int>(type: "integer", nullable: true),
                    GameName = table.Column<string>(type: "text", nullable: true),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BggImportQueue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cache_stats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_id = table.Column<Guid>(type: "uuid", maxLength: 50, nullable: false),
                    question_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    hit_count = table.Column<long>(type: "bigint", nullable: false),
                    miss_count = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_hit_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cache_stats", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cost_scenarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MessagesPerDay = table.Column<int>(type: "integer", nullable: false),
                    ActiveUsers = table.Column<int>(type: "integer", nullable: false),
                    AvgTokensPerRequest = table.Column<int>(type: "integer", nullable: false),
                    CostPerRequest = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    DailyProjection = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MonthlyProjection = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Warnings = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_scenarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "custom_rag_pipelines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    pipeline_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    is_template = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    access_tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_rag_pipelines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_queue_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    html_body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    max_retries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_queue_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    html_body = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entity_links",
                schema: "entity_relationships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_bidirectional = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    is_admin_approved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_bgg_imported = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "extracted_facts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FactData = table.Column<string>(type: "jsonb", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelUsed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reviewer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsReviewed = table.Column<bool>(type: "boolean", nullable: false),
                    ExtractionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extracted_facts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_analytics_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_analytics_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_designers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_designers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_mechanics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_mechanics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_night_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    max_players = table.Column<int>(type: "integer", nullable: true),
                    game_ids = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reminder_24h_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reminder_1h_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_night_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_night_playlists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scheduled_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    creator_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_shared = table.Column<bool>(type: "boolean", nullable: false),
                    games_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_night_playlists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_publishers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_publishers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_strategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Upvotes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Tags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "insight_feedback",
                schema: "administration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insight_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    insight_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_relevant = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insight_feedback", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "llm_request_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    request_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Manual"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_tokens = table.Column<int>(type: "integer", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_streaming = table.Column<bool>(type: "boolean", nullable: false),
                    is_free_model = table.Column<bool>(type: "boolean", nullable: false),
                    session_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_anonymized = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    user_region = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_request_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "LlmSystemConfigs",
                schema: "SystemConfiguration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CircuitBreakerFailureThreshold = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    CircuitBreakerOpenDurationSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    CircuitBreakerSuccessThreshold = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    DailyBudgetUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false, defaultValue: 10.00m),
                    MonthlyBudgetUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false, defaultValue: 100.00m),
                    FallbackChainJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmSystemConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "model_change_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PreviousModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NewModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AffectedStrategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsAutomatic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_change_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "model_compatibility_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Alternatives = table.Column<string[]>(type: "jsonb", nullable: false),
                    ContextWindow = table.Column<int>(type: "integer", nullable: false),
                    Strengths = table.Column<string[]>(type: "jsonb", nullable: false),
                    IsCurrentlyAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_compatibility_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailOnDocumentReady = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EmailOnDocumentFailed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EmailOnRetryAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PushOnDocumentReady = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PushOnDocumentFailed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PushOnRetryAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PushEndpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PushP256dhKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PushAuthKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    InAppOnDocumentReady = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InAppOnDocumentFailed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InAppOnRetryAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InAppOnGameNightInvitation = table.Column<bool>(type: "boolean", nullable: false),
                    EmailOnGameNightInvitation = table.Column<bool>(type: "boolean", nullable: false),
                    PushOnGameNightInvitation = table.Column<bool>(type: "boolean", nullable: false),
                    EmailOnGameNightReminder = table.Column<bool>(type: "boolean", nullable: false),
                    PushOnGameNightReminder = table.Column<bool>(type: "boolean", nullable: false),
                    SlackEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SlackOnDocumentReady = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SlackOnDocumentFailed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SlackOnRetryAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SlackOnGameNightInvitation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SlackOnGameNightReminder = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SlackOnShareRequestCreated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SlackOnShareRequestApproved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SlackOnBadgeEarned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_queue_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notification_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    slack_channel_target = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    slack_team_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    max_retries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_queue_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "playground_test_scenarios",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    expected_outcome = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    agent_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    messages = table.Column<string>(type: "jsonb", nullable: false),
                    tags = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playground_test_scenarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processing_queue_config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_paused = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    max_concurrent_workers = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_queue_config", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prompt_evaluation_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: false),
                    dataset_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_queries = table.Column<int>(type: "integer", nullable: false),
                    accuracy = table.Column<double>(type: "double precision", nullable: false),
                    relevance = table.Column<double>(type: "double precision", nullable: false),
                    completeness = table.Column<double>(type: "double precision", nullable: false),
                    clarity = table.Column<double>(type: "double precision", nullable: false),
                    citation_quality = table.Column<double>(type: "double precision", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    query_results_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_evaluation_results", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rag_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pipeline_definition_json = table.Column<string>(type: "jsonb", nullable: false),
                    test_query = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    executed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    total_duration_ms = table.Column<int>(type: "integer", nullable: false),
                    total_tokens_used = table.Column<int>(type: "integer", nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    blocks_executed = table.Column<int>(type: "integer", nullable: false),
                    blocks_failed = table.Column<int>(type: "integer", nullable: false),
                    final_response = table.Column<string>(type: "text", nullable: true),
                    execution_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    events_json = table.Column<string>(type: "jsonb", nullable: false),
                    config_overrides_json = table.Column<string>(type: "jsonb", nullable: true),
                    parent_execution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_rag_executions_rag_executions_parent_execution_id",
                        column: x => x.parent_execution_id,
                        principalTable: "rag_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "rag_executions",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    query = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    agent_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    agent_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    strategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_playground = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    total_latency_ms = table.Column<int>(type: "integer", nullable: false),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    cache_hit = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    execution_trace = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_executions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rag_pipeline_strategies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nodes_json = table.Column<string>(type: "jsonb", nullable: false),
                    edges_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_template = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    template_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tags_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_pipeline_strategies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "resource_forecasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GrowthPattern = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MonthlyGrowthRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CurrentUsers = table.Column<int>(type: "integer", nullable: false),
                    CurrentDbSizeGb = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    CurrentDailyTokens = table.Column<long>(type: "bigint", nullable: false),
                    CurrentCacheMb = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    CurrentVectorEntries = table.Column<long>(type: "bigint", nullable: false),
                    DbPerUserMb = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    TokensPerUserPerDay = table.Column<int>(type: "integer", nullable: false),
                    CachePerUserMb = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    VectorsPerUser = table.Column<int>(type: "integer", nullable: false),
                    ProjectionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RecommendationsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ProjectedMonthlyCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_forecasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "session_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_index = table.Column<int>(type: "integer", nullable: true),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attachment_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    blob_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    thumbnail_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_attachments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "session_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_index = table.Column<int>(type: "integer", nullable: false),
                    trigger_type = table.Column<int>(type: "integer", nullable: false),
                    trigger_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    delta_data_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_checkpoint = table.Column<bool>(type: "boolean", nullable: false),
                    turn_index = table.Column<int>(type: "integer", nullable: false),
                    phase_index = table.Column<int>(type: "integer", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_player_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "share_request_limit_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    max_pending_requests = table.Column<int>(type: "integer", nullable: false),
                    max_requests_per_month = table.Column<int>(type: "integer", nullable: false),
                    cooldown_after_rejection_seconds = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_request_limit_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "similarity_audit_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PairId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceGame = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CheckName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    Threshold = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_similarity_audit_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "slack_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slack_user_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    slack_team_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    slack_team_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bot_access_token = table.Column<string>(type: "text", nullable: false),
                    dm_channel_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    connected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    disconnected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slack_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "slack_team_channel_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    webhook_url = table.Column<string>(type: "text", nullable: false),
                    notification_types = table.Column<string>(type: "jsonb", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    overrides_default = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slack_team_channel_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "strategy_model_mapping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Strategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PrimaryModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FallbackModels = table.Column<string[]>(type: "text[]", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsCustomizable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AdminOnly = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_model_mapping", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "strategy_patterns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pattern_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    applicable_phase = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    evaluation_score = table.Column<double>(type: "double precision", nullable: true),
                    board_conditions_json = table.Column<string>(type: "jsonb", nullable: true),
                    move_sequence_json = table.Column<string>(type: "jsonb", nullable: true),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_patterns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tier_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    max_private_games = table.Column<int>(type: "integer", nullable: false),
                    max_pdf_uploads_per_month = table.Column<int>(type: "integer", nullable: false),
                    max_pdf_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    max_agents = table.Column<int>(type: "integer", nullable: false),
                    max_agent_queries_per_day = table.Column<int>(type: "integer", nullable: false),
                    max_session_queries = table.Column<int>(type: "integer", nullable: false),
                    max_session_players = table.Column<int>(type: "integer", nullable: false),
                    max_photos_per_session = table.Column<int>(type: "integer", nullable: false),
                    session_save_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    max_catalog_proposals_per_week = table.Column<int>(type: "integer", nullable: false),
                    llm_model_tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tier_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tier_strategy_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Strategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tier_strategy_access", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "token_tiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tokens_per_month = table.Column<int>(type: "integer", nullable: false),
                    tokens_per_day = table.Column<int>(type: "integer", nullable: false),
                    messages_per_day = table.Column<int>(type: "integer", nullable: false),
                    max_collection_size = table.Column<int>(type: "integer", nullable: false),
                    max_pdf_uploads_per_month = table.Column<int>(type: "integer", nullable: false),
                    max_agents_created = table.Column<int>(type: "integer", nullable: false),
                    daily_credits_limit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    weekly_credits_limit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    monthly_fee = table.Column<decimal>(type: "numeric(10,2)", precision: 18, scale: 2, nullable: false),
                    cost_per_extra_token = table.Column<decimal>(type: "numeric(10,6)", precision: 18, scale: 6, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_token_tiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tool_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolkit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tool_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tool_type = table.Column<int>(type: "integer", nullable: false),
                    state_data_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "toolbox_templates",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tools_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    phases_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    shared_context_defaults_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolbox_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "toolboxes",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    shared_context = table.Column<string>(type: "jsonb", nullable: false),
                    current_phase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolboxes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "toolkit_session_states",
                schema: "session_tracking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolkit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    widget_states = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolkit_session_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "toolkits",
                schema: "game_toolkit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolkits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "turn_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_order_json = table.Column<string>(type: "jsonb", nullable: false),
                    current_index = table.Column<int>(type: "integer", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_turn_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_ai_consents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsentedToAiProcessing = table.Column<bool>(type: "boolean", nullable: false),
                    ConsentedToExternalProviders = table.Column<bool>(type: "boolean", nullable: false),
                    ConsentedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_ai_consents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_token_usage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tokens_used = table.Column<int>(type: "integer", nullable: false),
                    messages_count = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(10,2)", precision: 18, scale: 4, nullable: false),
                    last_reset = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    daily_credits_used = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    weekly_credits_used = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    last_daily_reset = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_weekly_reset = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    is_near_limit = table.Column<bool>(type: "boolean", nullable: false),
                    warnings = table.Column<string>(type: "jsonb", nullable: false),
                    history = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_token_usage", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Tier = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDemoAccount = table.Column<bool>(type: "boolean", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                    EmailNotifications = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Theme = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "system"),
                    DataRetentionDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    TotpSecretEncrypted = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsTwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TwoFactorEnabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EmailVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationGracePeriodEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendReason = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ExperiencePoints = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsContributor = table.Column<bool>(type: "boolean", nullable: false),
                    Interests = table.Column<List<string>>(type: "jsonb", nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OnboardingWizardSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OnboardingDismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OnboardingCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    OnboardingSkipped = table.Column<bool>(type: "boolean", nullable: false),
                    OnboardingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "validation_accuracy_baselines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    context = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    dataset_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    measured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    true_positives = table.Column<int>(type: "integer", nullable: false),
                    true_negatives = table.Column<int>(type: "integer", nullable: false),
                    false_positives = table.Column<int>(type: "integer", nullable: false),
                    false_negatives = table.Column<int>(type: "integer", nullable: false),
                    total_cases = table.Column<int>(type: "integer", nullable: false),
                    precision = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    recall = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    f1_score = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    accuracy = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    specificity = table.Column<double>(type: "double precision", precision: 5, scale: 4, nullable: false),
                    matthews_correlation = table.Column<double>(type: "double precision", precision: 6, scale: 4, nullable: false),
                    meets_baseline = table.Column<bool>(type: "boolean", nullable: false),
                    quality_level = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recommendations_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_validation_accuracy_baselines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "whiteboard_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strokes_json = table.Column<string>(type: "jsonb", nullable: false),
                    structured_json = table.Column<string>(type: "jsonb", nullable: false),
                    last_modified_by = table.Column<Guid>(type: "uuid", nullable: false),
                    last_modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whiteboard_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_error_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    execution_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    error_message = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    node_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stack_trace = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_error_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ab_test_variants",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ab_test_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    response = table.Column<string>(type: "text", nullable: true),
                    tokens_used = table.Column<int>(type: "integer", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    failed = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    evaluator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    eval_accuracy = table.Column<int>(type: "integer", nullable: true),
                    eval_completeness = table.Column<int>(type: "integer", nullable: true),
                    eval_clarity = table.Column<int>(type: "integer", nullable: true),
                    eval_tone = table.Column<int>(type: "integer", nullable: true),
                    eval_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    eval_evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ab_test_variants", x => x.id);
                    table.ForeignKey(
                        name: "FK_ab_test_variants_ab_test_sessions_ab_test_session_id",
                        column: x => x.ab_test_session_id,
                        principalSchema: "knowledge_base",
                        principalTable: "ab_test_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admin_report_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    output_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    execution_metadata = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_report_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_admin_report_executions_admin_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "admin_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bgg_id = table.Column<int>(type: "integer", nullable: true),
                    agent_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    year_published = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    min_players = table.Column<int>(type: "integer", nullable: false),
                    max_players = table.Column<int>(type: "integer", nullable: false),
                    playing_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    min_age = table.Column<int>(type: "integer", nullable: false),
                    complexity_rating = table.Column<decimal>(type: "numeric(3,2)", nullable: true),
                    average_rating = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    thumbnail_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    GameDataStatus = table.Column<int>(type: "integer", nullable: false),
                    rules_content = table.Column<string>(type: "text", nullable: true),
                    rules_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RulesExternalUrl = table.Column<string>(type: "text", nullable: true),
                    BggRawData = table.Column<string>(type: "text", nullable: true),
                    HasUploadedPdf = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_rag_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_games", x => x.id);
                    table.CheckConstraint("chk_shared_games_complexity", "complexity_rating IS NULL OR (complexity_rating >= 1.0 AND complexity_rating <= 5.0)");
                    table.CheckConstraint("chk_shared_games_min_age", "min_age >= 0");
                    table.CheckConstraint("chk_shared_games_players", "min_players > 0 AND max_players >= min_players");
                    table.CheckConstraint("chk_shared_games_playing_time", "playing_time_minutes > 0");
                    table.CheckConstraint("chk_shared_games_rating", "average_rating IS NULL OR (average_rating >= 1.0 AND average_rating <= 10.0)");
                    table.CheckConstraint("chk_shared_games_year_published", "year_published > 1900 AND year_published <= 2100");
                    table.ForeignKey(
                        name: "FK_shared_games_agent_definitions_agent_definition_id",
                        column: x => x.agent_definition_id,
                        principalSchema: "knowledge_base",
                        principalTable: "agent_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "typology_prompt_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    typology_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_typology_prompt_templates", x => x.id);
                    table.CheckConstraint("ck_typology_prompt_templates_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_typology_prompt_templates_agent_typologies_typology_id",
                        column: x => x.typology_id,
                        principalTable: "agent_typologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_badges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    badge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    earned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    triggering_share_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_displayed = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_badges", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_badges_badges_badge_id",
                        column: x => x.badge_id,
                        principalTable: "badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "game_night_rsvps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_night_rsvps", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_night_rsvps_game_night_events_event_id",
                        column: x => x.event_id,
                        principalTable: "game_night_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "toolbox_phases",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active_tool_ids = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolbox_phases", x => x.id);
                    table.ForeignKey(
                        name: "FK_toolbox_phases_toolboxes_toolbox_id",
                        column: x => x.toolbox_id,
                        principalSchema: "game_toolbox",
                        principalTable: "toolboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "toolbox_tools",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    state = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolbox_tools", x => x.id);
                    table.ForeignKey(
                        name: "FK_toolbox_tools_toolboxes_toolbox_id",
                        column: x => x.toolbox_id,
                        principalSchema: "game_toolbox",
                        principalTable: "toolboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "toolkit_widgets",
                schema: "game_toolkit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolkit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolkit_widgets", x => x.id);
                    table.ForeignKey(
                        name: "FK_toolkit_widgets_toolkits_toolkit_id",
                        column: x => x.toolkit_id,
                        principalSchema: "game_toolkit",
                        principalTable: "toolkits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_test_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    typology_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_override = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    model_used = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    query = table.Column<string>(type: "text", nullable: false),
                    response = table.Column<string>(type: "text", nullable: false),
                    confidence_score = table.Column<double>(type: "double precision", nullable: false),
                    tokens_used = table.Column<int>(type: "integer", nullable: false),
                    cost_estimate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    citations_json = table.Column<string>(type: "jsonb", nullable: true),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    executed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_saved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_test_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_test_results_agent_typologies_typology_id",
                        column: x => x.typology_id,
                        principalTable: "agent_typologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_test_results_users_executed_by",
                        column: x => x.executed_by,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    KeyName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Scopes = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_keys_users_RevokedBy",
                        column: x => x.RevokedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_api_keys_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversation_memory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    message_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_memory", x => x.id);
                    table.ForeignKey(
                        name: "FK_conversation_memory_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_verifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvalidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_verifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_verifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_labels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    is_predefined = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_game_labels_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invitation_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_invitation_tokens_users_accepted_by_user_id",
                        column: x => x.accepted_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_invitation_tokens_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "library_share_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    privacy_level = table.Column<int>(type: "integer", nullable: false),
                    include_notes = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    view_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_library_share_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_library_share_links_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "llm_cost_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_tokens = table.Column<int>(type: "integer", nullable: false),
                    input_cost_usd = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    output_cost_usd = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_cost_usd = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    request_date = table.Column<DateOnly>(type: "date", nullable: false),
                    request_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Manual")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_cost_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_llm_cost_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "n8n_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    WebhookUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTestResult = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_n8n_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_n8n_configs_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "oauth_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderUserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AccessTokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    RefreshTokenEncrypted = table.Column<string>(type: "text", nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_oauth_accounts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "private_games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bgg_id = table.Column<int>(type: "integer", nullable: true),
                    agent_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    year_published = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    min_players = table.Column<int>(type: "integer", nullable: false),
                    max_players = table.Column<int>(type: "integer", nullable: false),
                    playing_time_minutes = table.Column<int>(type: "integer", nullable: true),
                    min_age = table.Column<int>(type: "integer", nullable: true),
                    complexity_rating = table.Column<decimal>(type: "numeric(3,2)", nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    thumbnail_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bgg_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_private_games", x => x.id);
                    table.CheckConstraint("chk_private_games_complexity", "complexity_rating IS NULL OR (complexity_rating >= 1.0 AND complexity_rating <= 5.0)");
                    table.CheckConstraint("chk_private_games_min_age", "min_age IS NULL OR min_age >= 0");
                    table.CheckConstraint("chk_private_games_players", "min_players > 0 AND max_players >= min_players AND max_players <= 100");
                    table.CheckConstraint("chk_private_games_playing_time", "playing_time_minutes IS NULL OR playing_time_minutes > 0");
                    table.CheckConstraint("chk_private_games_year_published", "year_published IS NULL OR (year_published > 1900 AND year_published <= 2100)");
                    table.ForeignKey(
                        name: "FK_private_games_agent_definitions_agent_definition_id",
                        column: x => x.agent_definition_id,
                        principalSchema: "knowledge_base",
                        principalTable: "agent_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_private_games_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prompt_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prompt_templates_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rag_user_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_user_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rag_user_configs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "system_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RequiresRestart = table.Column<bool>(type: "boolean", nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PreviousValue = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    LastToggledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_system_configurations_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_system_configurations_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "temp_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temp_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_temp_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "used_totp_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TimeStep = table.Column<long>(type: "bigint", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_used_totp_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_used_totp_codes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_achievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_achievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_achievements_achievements_AchievementId",
                        column: x => x.AchievementId,
                        principalTable: "achievements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_achievements_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_backup_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_backup_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_backup_codes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_collection_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_collection_entries", x => x.Id);
                    table.CheckConstraint("CK_UserCollectionEntries_EntityType", "\"EntityType\" IN ('Player', 'Event', 'Session', 'Agent', 'Document', 'ChatSession')");
                    table.ForeignKey(
                        name: "FK_user_collection_entries_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_rate_limit_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    max_pending_requests = table.Column<int>(type: "integer", nullable: true),
                    max_requests_per_month = table.Column<int>(type: "integer", nullable: true),
                    cooldown_after_rejection_seconds = table.Column<long>(type: "bigint", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_rate_limit_overrides", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_rate_limit_overrides_users_created_by_admin_id",
                        column: x => x.created_by_admin_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_rate_limit_overrides_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contributors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary_contributor = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contributors", x => x.id);
                    table.ForeignKey(
                        name: "FK_contributors_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_errata",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    page_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    published_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_errata", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_errata_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_faqs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    answer = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    upvote_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_faqs", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_faqs_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_state_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    schema_json = table.Column<string>(type: "jsonb", nullable: true),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "1.0"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    source = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_state_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_state_templates_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Publisher = table.Column<string>(type: "text", nullable: true),
                    YearPublished = table.Column<int>(type: "integer", nullable: true),
                    MinPlayers = table.Column<int>(type: "integer", nullable: true),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: true),
                    MinPlayTimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    MaxPlayTimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    BggId = table.Column<int>(type: "integer", nullable: true),
                    BggMetadata = table.Column<string>(type: "text", nullable: true),
                    IconUrl = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    VersionType = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    VersionNumber = table.Column<string>(type: "text", nullable: true),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_games_shared_games_SharedGameId",
                        column: x => x.SharedGameId,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "mechanic_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    game_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    summary_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    mechanics_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    victory_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    resources_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    phases_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    questions_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    summary_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    mechanics_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    victory_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    resources_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    phases_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    questions_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mechanic_drafts", x => x.id);
                    table.ForeignKey(
                        name: "FK_mechanic_drafts_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quick_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    emoji = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_generated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quick_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_quick_questions_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rulebook_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    key_mechanics_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    victory_conditions_json = table.Column<string>(type: "jsonb", nullable: true),
                    resources_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    game_phases_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    common_questions_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    key_concepts_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    generated_faqs_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    game_state_schema_json = table.Column<string>(type: "jsonb", nullable: true),
                    completion_status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    missing_sections_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "1.0"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    source = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    analyzed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rulebook_analyses", x => x.id);
                    table.ForeignKey(
                        name: "FK_rulebook_analyses_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_game_categories",
                columns: table => new
                {
                    game_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_categories", x => new { x.game_category_id, x.shared_game_id });
                    table.ForeignKey(
                        name: "FK_shared_game_categories_game_categories_game_category_id",
                        column: x => x.game_category_id,
                        principalTable: "game_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_game_categories_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_game_delete_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_delete_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_shared_game_delete_requests_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_game_designers",
                columns: table => new
                {
                    game_designer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_designers", x => new { x.game_designer_id, x.shared_game_id });
                    table.ForeignKey(
                        name: "FK_shared_game_designers_game_designers_game_designer_id",
                        column: x => x.game_designer_id,
                        principalTable: "game_designers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_game_designers_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_game_mechanics",
                columns: table => new
                {
                    game_mechanic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_mechanics", x => new { x.game_mechanic_id, x.shared_game_id });
                    table.ForeignKey(
                        name: "FK_shared_game_mechanics_game_mechanics_game_mechanic_id",
                        column: x => x.game_mechanic_id,
                        principalTable: "game_mechanics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_game_mechanics_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_game_publishers",
                columns: table => new
                {
                    game_publisher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_publishers", x => new { x.game_publisher_id, x.shared_game_id });
                    table.ForeignKey(
                        name: "FK_shared_game_publishers_game_publishers_game_publisher_id",
                        column: x => x.game_publisher_id,
                        principalTable: "game_publishers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_game_publishers_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wishlist_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    TargetPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wishlist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wishlist_items_shared_games_GameId",
                        column: x => x.GameId,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wishlist_items_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "api_key_usage_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    http_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    response_time_ms = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_key_usage_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_api_key_usage_logs_api_keys_key_id",
                        column: x => x.key_id,
                        principalTable: "api_keys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "access_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_access_requests_invitation_tokens_invitation_id",
                        column: x => x.invitation_id,
                        principalTable: "invitation_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_access_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProposalMigrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrivateGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Choice = table.Column<int>(type: "integer", nullable: false, comment: "0 = Pending, 1 = LinkToCatalog, 2 = KeepPrivate"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ChoiceAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalMigrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalMigrations_private_games_PrivateGameId",
                        column: x => x.PrivateGameId,
                        principalTable: "private_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "share_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_shared_game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_private_game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    status_before_review = table.Column<int>(type: "integer", nullable: true),
                    contribution_type = table.Column<int>(type: "integer", nullable: false),
                    user_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    admin_feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewing_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_lock_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_share_requests_private_games_source_private_game_id",
                        column: x => x.source_private_game_id,
                        principalTable: "private_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_share_requests_shared_games_source_game_id",
                        column: x => x.source_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_share_requests_shared_games_target_shared_game_id",
                        column: x => x.target_shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "prompt_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ChangeNotes = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivationReason = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prompt_versions_prompt_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "prompt_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prompt_versions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contribution_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    contributed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    share_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_ids = table.Column<string>(type: "jsonb", nullable: true),
                    includes_game_data = table.Column<bool>(type: "boolean", nullable: false),
                    includes_metadata = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contribution_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_contribution_records_contributors_contributor_id",
                        column: x => x.contributor_id,
                        principalTable: "contributors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StrategyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StrategyParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastInvokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvocationCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agents_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_agents_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "chat_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_library_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    agent_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    agent_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    agent_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    agent_config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    messages_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_sessions_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentType = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MessagesJson = table.Column<string>(type: "text", nullable: false),
                    ConversationSummary = table.Column<string>(type: "text", nullable: true),
                    LastSummarizedMessageCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatThreads_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatThreads_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chunked_upload_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrivateGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TotalFileSize = table.Column<long>(type: "bigint", nullable: false),
                    TotalChunks = table.Column<int>(type: "integer", nullable: false),
                    ReceivedChunks = table.Column<int>(type: "integer", nullable: false),
                    TempDirectory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ReceivedChunkIndices = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunked_upload_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chunked_upload_sessions_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_chunked_upload_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DocumentsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_collections_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_collections_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WinnerName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PlayersJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessions_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameSessions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GameToolkits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrivateGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OverridesTurnOrder = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OverridesScoreboard = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OverridesDiceSet = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DiceToolsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CardToolsJson = table.Column<string>(type: "jsonb", nullable: true),
                    TimerToolsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CounterToolsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ScoringTemplateJson = table.Column<string>(type: "jsonb", nullable: true),
                    TurnTemplateJson = table.Column<string>(type: "jsonb", nullable: true),
                    StateTemplate = table.Column<string>(type: "jsonb", nullable: true),
                    AgentConfig = table.Column<string>(type: "jsonb", nullable: true),
                    TemplateStatus = table.Column<int>(type: "integer", nullable: false),
                    IsTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewNotes = table.Column<string>(type: "text", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameToolkits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameToolkits_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameToolkits_private_games_PrivateGameId",
                        column: x => x.PrivateGameId,
                        principalTable: "private_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_game_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    game_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    toolkit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visibility = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    current_turn_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paused_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalPausedDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    scoring_config_json = table.Column<string>(type: "jsonb", nullable: false),
                    game_state_json = table.Column<string>(type: "jsonb", nullable: true),
                    turn_order_json = table.Column<string>(type: "jsonb", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    agent_mode = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    chat_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_game_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_game_sessions_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_live_game_sessions_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "play_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ScoringConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_play_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_play_records_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_play_records_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rule_conflict_faqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConflictType = table.Column<int>(type: "integer", nullable: false),
                    Pattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_conflict_faqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rule_conflict_faqs_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rule_specs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    ParentVersionId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    MergedFromVersionIds = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    GameEntityId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_specs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rule_specs_games_GameEntityId",
                        column: x => x.GameEntityId,
                        principalTable: "games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_rule_specs_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rule_specs_rule_specs_ParentVersionId",
                        column: x => x.ParentVersionId,
                        principalTable: "rule_specs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rule_specs_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "rulespec_comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AtomId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CommentText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: true),
                    LineContext = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ParentCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MentionedUserIds = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rulespec_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rulespec_comments_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rulespec_comments_rulespec_comments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "rulespec_comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rulespec_comments_users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rulespec_comments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    session_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    session_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    finalized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    InviteToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InviteExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_sessions_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_tracking_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "share_request_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    attached_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_request_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_share_request_documents_share_requests_share_request_id",
                        column: x => x.share_request_id,
                        principalTable: "share_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prompt_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Details = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prompt_audit_logs_prompt_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "prompt_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prompt_audit_logs_prompt_versions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "prompt_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prompt_audit_logs_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    llm_provider = table.Column<int>(type: "integer", nullable: false),
                    llm_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    agent_mode = table.Column<int>(type: "integer", nullable: false),
                    selected_document_ids_json = table.Column<string>(type: "jsonb", nullable: true),
                    temperature = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 0.7m),
                    max_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 4096),
                    system_prompt_override = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_configurations", x => x.id);
                    table.CheckConstraint("ck_agent_configurations_max_tokens", "max_tokens > 0 AND max_tokens <= 32000");
                    table.CheckConstraint("ck_agent_configurations_temperature", "temperature >= 0.0 AND temperature <= 2.0");
                    table.ForeignKey(
                        name: "FK_agent_configurations_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chats_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chats_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chats_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "share_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    thread_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    access_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_share_links_ChatThreads_thread_id",
                        column: x => x.thread_id,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_share_links_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chat_thread_collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    ChatThreadId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_thread_collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chat_thread_collections_ChatThreads_ChatThreadId",
                        column: x => x.ChatThreadId,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_thread_collections_document_collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "document_collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pdf_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    processing_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Pending"),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PageCount = table.Column<int>(type: "integer", nullable: true),
                    CharacterCount = table.Column<int>(type: "integer", nullable: true),
                    ProcessingError = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    error_category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    failed_at_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ExtractedTables = table.Column<string>(type: "text", nullable: true),
                    ExtractedDiagrams = table.Column<string>(type: "text", nullable: true),
                    AtomicRules = table.Column<string>(type: "text", nullable: true),
                    TableCount = table.Column<int>(type: "integer", nullable: true),
                    DiagramCount = table.Column<int>(type: "integer", nullable: true),
                    AtomicRuleCount = table.Column<int>(type: "integer", nullable: true),
                    ProcessingProgressJson = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: false),
                    language_confidence = table.Column<double>(type: "double precision", nullable: true),
                    language_override = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "base"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContributorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrivateGameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    document_category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Rulebook"),
                    base_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    copyright_disclaimer_accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    copyright_disclaimer_accepted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active_for_rag = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    version_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    processing_priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Normal"),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    uploading_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    extracting_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    chunking_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    embedding_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    indexing_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pdf_documents_document_collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "document_collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pdf_documents_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pdf_documents_pdf_documents_base_document_id",
                        column: x => x.base_document_id,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pdf_documents_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypologyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentGameStateJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_sessions_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_sessions_agent_typologies_TypologyId",
                        column: x => x.TypologyId,
                        principalTable: "agent_typologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_sessions_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_sessions_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arbitro_validation_feedback",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    validation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    accuracy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ai_decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ai_confidence = table.Column<double>(type: "double precision", nullable: false),
                    had_conflicts = table.Column<bool>(type: "boolean", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arbitro_validation_feedback", x => x.id);
                    table.ForeignKey(
                        name: "FK_arbitro_validation_feedback_GameSessions_game_session_id",
                        column: x => x.game_session_id,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_arbitro_validation_feedback_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "decisore_move_feedback",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    quality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    suggestion_followed = table.Column<bool>(type: "boolean", nullable: false),
                    top_suggested_move = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    position_strength = table.Column<double>(type: "double precision", nullable: false),
                    analysis_depth = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decisore_move_feedback", x => x.id);
                    table.ForeignKey(
                        name: "FK_decisore_move_feedback_GameSessions_game_session_id",
                        column: x => x.game_session_id,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_decisore_move_feedback_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_session_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_state_json = table.Column<string>(type: "jsonb", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_session_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_session_states_GameSessions_game_session_id",
                        column: x => x.game_session_id,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EncryptedContent = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    IsRevealed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ObscuredText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionNotes_GameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pin = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    link_token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    max_uses = table.Column<int>(type: "integer", nullable: false),
                    current_uses = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_invites", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_invites_live_game_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    registered_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agent_access_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    connection_token = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_participants_live_game_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "session_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    team_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    current_rank = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_teams_live_game_sessions_live_game_session_id",
                        column: x => x.live_game_session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_players_play_records_PlayRecordId",
                        column: x => x.PlayRecordId,
                        principalTable: "play_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_record_players_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "rule_atoms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    RuleSpecId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Section = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_atoms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rule_atoms_rule_specs_RuleSpecId",
                        column: x => x.RuleSpecId,
                        principalTable: "rule_specs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_checkpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SnapshotData = table.Column<string>(type: "jsonb", nullable: false),
                    DiaryEventCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_checkpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_checkpoints_session_tracking_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_events_session_tracking_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_owner = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Player"),
                    is_ready = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    join_order = table.Column<int>(type: "integer", nullable: false),
                    final_rank = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_participants_session_tracking_sessions_ses~",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionDecks",
                schema: "session_tracking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeckType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastShuffledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DrawPileJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    DiscardPileJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    HandsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionDecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionDecks_session_tracking_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsInvalidated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chat_logs_chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_logs_users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_chat_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "pdf_processing_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_seconds = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    pdf_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    page_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_processing_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdf_processing_metrics_pdf_documents_pdf_document_id",
                        column: x => x.pdf_document_id,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "processing_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Queued"),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    current_step = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_retries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_processing_jobs_pdf_documents_pdf_document_id",
                        column: x => x.pdf_document_id,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_processing_jobs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shared_game_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approval_status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approval_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_shared_game_documents_pdf_documents_pdf_document_id",
                        column: x => x.pdf_document_id,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shared_game_documents_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "text_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    PdfDocumentId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_text_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_text_chunks_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_text_chunks_pdf_documents_PdfDocumentId",
                        column: x => x.PdfDocumentId,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_library_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    private_game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CustomAgentConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    CustomPdfUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CustomPdfUploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomPdfFileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    CustomPdfOriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PrivatePdfId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnershipDeclaredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentState = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StateChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StateNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TimesPlayed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastPlayed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WinRate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    AvgDuration = table.Column<int>(type: "integer", nullable: true),
                    CompetitiveSessions = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_library_entries", x => x.Id);
                    table.CheckConstraint("CK_UserLibraryEntry_GameSource", "(shared_game_id IS NOT NULL AND private_game_id IS NULL) OR (shared_game_id IS NULL AND private_game_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_user_library_entries_pdf_documents_PrivatePdfId",
                        column: x => x.PrivatePdfId,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_library_entries_private_games_private_game_id",
                        column: x => x.private_game_id,
                        principalTable: "private_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_library_entries_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_library_entries_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vector_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    PdfDocumentId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCharacters = table.Column<int>(type: "integer", nullable: false),
                    IndexingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IndexingError = table.Column<string>(type: "text", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmbeddingDimensions = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vector_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vector_documents_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vector_documents_pdf_documents_PdfDocumentId",
                        column: x => x.PdfDocumentId,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_game_state_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    board_state_json = table.Column<string>(type: "jsonb", nullable: false),
                    turn_number = table.Column<int>(type: "integer", nullable: false),
                    active_player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_game_state_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_game_state_snapshots_agent_sessions_agent_session_id",
                        column: x => x.agent_session_id,
                        principalTable: "agent_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_state_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_json = table.Column<string>(type: "jsonb", nullable: false),
                    turn_number = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_state_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_state_snapshots_game_session_states_session_state_id",
                        column: x => x.session_state_id,
                        principalTable: "game_session_states",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    current_rank = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_players", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_players_live_game_sessions_live_game_session_id",
                        column: x => x.live_game_session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_players_session_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "session_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_session_players_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "record_scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dimension = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_scores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_scores_record_players_RecordPlayerId",
                        column: x => x.RecordPlayerId,
                        principalTable: "record_players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_card_draws",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deck_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    deck_id = table.Column<Guid>(type: "uuid", nullable: true),
                    card_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_card_draws", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_card_draws_session_tracking_participants_p~",
                        column: x => x.participant_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_card_draws_session_tracking_sessions_sessi~",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    message_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    turn_number = table.Column<int>(type: "integer", nullable: true),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    agent_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    confidence = table.Column<float>(type: "real", nullable: true),
                    citations_json = table.Column<string>(type: "text", nullable: true),
                    mentions_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_chat_messages_session_tracking_participant~",
                        column: x => x.sender_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_session_tracking_chat_messages_session_tracking_sessions_se~",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_dice_rolls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    formula = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rolls = table.Column<string>(type: "jsonb", nullable: false),
                    modifier = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total = table.Column<int>(type: "integer", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_dice_rolls", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_dice_rolls_session_tracking_participants_p~",
                        column: x => x.participant_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_dice_rolls_session_tracking_sessions_sessi~",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    thumbnail_file_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    turn_number = table.Column<int>(type: "integer", nullable: true),
                    is_shared_with_session = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_media", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_media_session_tracking_participants_partic~",
                        column: x => x.participant_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_media_session_tracking_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    template_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_notes_session_tracking_participants_partic~",
                        column: x => x.participant_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_notes_session_tracking_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_score_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    score_value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_score_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_score_entries_session_tracking_participant~",
                        column: x => x.participant_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_score_entries_session_tracking_sessions_se~",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                schema: "session_tracking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionDeckId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Suit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cards_SessionDecks_SessionDeckId",
                        column: x => x.SessionDeckId,
                        principalSchema: "session_tracking",
                        principalTable: "SessionDecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "processing_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    processing_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Pending"),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<double>(type: "double precision", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_processing_steps_processing_jobs_processing_job_id",
                        column: x => x.processing_job_id,
                        principalTable: "processing_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserLibraryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AdditionalInfo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_checklists", x => x.Id);
                    table.CheckConstraint("chk_game_checklists_display_order", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_game_checklists_user_library_entries_UserLibraryEntryId",
                        column: x => x.UserLibraryEntryId,
                        principalTable: "user_library_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserLibraryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    DidWin = table.Column<bool>(type: "boolean", nullable: true),
                    Players = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_sessions", x => x.Id);
                    table.CheckConstraint("chk_game_sessions_duration", "duration_minutes > 0");
                    table.ForeignKey(
                        name: "FK_game_sessions_user_library_entries_UserLibraryEntryId",
                        column: x => x.UserLibraryEntryId,
                        principalTable: "user_library_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_game_labels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserLibraryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_game_labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_game_labels_game_labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "game_labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_game_labels_user_library_entries_UserLibraryEntryId",
                        column: x => x.UserLibraryEntryId,
                        principalTable: "user_library_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_round_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round = table.Column<int>(type: "integer", nullable: false),
                    dimension = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_round_scores", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_round_scores_live_game_sessions_live_game_sess~",
                        column: x => x.live_game_session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_live_session_round_scores_session_players_player_id",
                        column: x => x.player_id,
                        principalTable: "session_players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_turn_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turn_index = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_index = table.Column<int>(type: "integer", nullable: true),
                    phase_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_turn_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_turn_records_live_game_sessions_live_game_sess~",
                        column: x => x.live_game_session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_live_session_turn_records_session_players_player_id",
                        column: x => x.player_id,
                        principalTable: "session_players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "step_log_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    processing_step_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Info"),
                    message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_step_log_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_step_log_entries_processing_steps_processing_step_id",
                        column: x => x.processing_step_id,
                        principalTable: "processing_steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ab_test_sessions_created_at",
                schema: "knowledge_base",
                table: "ab_test_sessions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_ab_test_sessions_created_by",
                schema: "knowledge_base",
                table: "ab_test_sessions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_ab_test_sessions_status",
                schema: "knowledge_base",
                table: "ab_test_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ab_test_variants_ab_test_session_id",
                schema: "knowledge_base",
                table: "ab_test_variants",
                column: "ab_test_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_ab_test_variants_model_id",
                schema: "knowledge_base",
                table: "ab_test_variants",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "IX_access_requests_email_status",
                table: "access_requests",
                columns: new[] { "email", "status" },
                unique: true,
                filter: "\"status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_access_requests_invitation_id",
                table: "access_requests",
                column: "invitation_id");

            migrationBuilder.CreateIndex(
                name: "IX_access_requests_requested_at",
                table: "access_requests",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "IX_access_requests_reviewed_by",
                table: "access_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_Category",
                table: "achievements",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_Code",
                table: "achievements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_IsActive",
                table: "achievements",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_admin_report_executions_report_id",
                table: "admin_report_executions",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_configurations_agent_id",
                table: "agent_configurations",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_configurations_current",
                table: "agent_configurations",
                columns: new[] { "agent_id", "is_current" },
                unique: true,
                filter: "is_current = true");

            migrationBuilder.CreateIndex(
                name: "IX_agent_definitions_created_at",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_agent_definitions_is_active",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_agent_definitions_name",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_definitions_type_value",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "type_value");

            migrationBuilder.CreateIndex(
                name: "IX_agent_feedback_Endpoint",
                table: "agent_feedback",
                column: "Endpoint");

            migrationBuilder.CreateIndex(
                name: "IX_agent_feedback_GameId",
                table: "agent_feedback",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_feedback_MessageId_UserId",
                table: "agent_feedback",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_feedback_UserId",
                table: "agent_feedback",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_game_state_snapshots_agent_session_id",
                table: "agent_game_state_snapshots",
                column: "agent_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_game_state_snapshots_created_at",
                table: "agent_game_state_snapshots",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_agent_game_state_snapshots_game_id",
                table: "agent_game_state_snapshots",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_game_state_snapshots_game_id_turn_number",
                table: "agent_game_state_snapshots",
                columns: new[] { "game_id", "turn_number" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_AgentId",
                table: "agent_sessions",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_GameId",
                table: "agent_sessions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_TypologyId",
                table: "agent_sessions",
                column: "TypologyId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_GameSessionId",
                table: "agent_sessions",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_GameSessionId_UserId_Unique",
                table: "agent_sessions",
                columns: new[] { "GameSessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_IsActive",
                table: "agent_sessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_UserId",
                table: "agent_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_UserId_IsActive",
                table: "agent_sessions",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_test_results_executed_at",
                table: "agent_test_results",
                column: "executed_at");

            migrationBuilder.CreateIndex(
                name: "ix_agent_test_results_executed_by",
                table: "agent_test_results",
                column: "executed_by");

            migrationBuilder.CreateIndex(
                name: "ix_agent_test_results_is_saved",
                table: "agent_test_results",
                column: "is_saved");

            migrationBuilder.CreateIndex(
                name: "ix_agent_test_results_typology_executed_at",
                table: "agent_test_results",
                columns: new[] { "typology_id", "executed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_test_results_typology_id",
                table: "agent_test_results",
                column: "typology_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_typologies_created_by",
                table: "agent_typologies",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_agent_typologies_name",
                table: "agent_typologies",
                column: "name",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_agent_typologies_status",
                table: "agent_typologies",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_agents_CreatedByUserId",
                table: "agents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_CreatedByUserId_Name",
                table: "agents",
                columns: new[] { "CreatedByUserId", "Name" },
                unique: true,
                filter: "\"CreatedByUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameId_CreatedByUserId",
                table: "agents",
                columns: new[] { "GameId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameId_Type",
                table: "agents",
                columns: new[] { "GameId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_agents_IsActive",
                table: "agents",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_agents_LastInvokedAt",
                table: "agents",
                column: "LastInvokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_agents_Name",
                table: "agents",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_agents_SharedGameId",
                table: "agents",
                column: "SharedGameId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_Type",
                table: "agents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_CreatedAt",
                table: "ai_request_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_Endpoint",
                table: "ai_request_logs",
                column: "Endpoint");

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_GameId",
                table: "ai_request_logs",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_request_logs_UserId",
                table: "ai_request_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiModelConfigurations_IsPrimary_IsActive",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                columns: new[] { "IsPrimary", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AiModelConfigurations_ModelId",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                column: "ModelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiModelConfigurations_Priority",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_AiModelConfigurations_TierRouting",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                columns: new[] { "applicable_tier", "environment_type", "is_default_for_tier" });

            migrationBuilder.CreateIndex(
                name: "ix_alert_configurations_category",
                table: "alert_configurations",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_alert_configurations_config_key",
                table: "alert_configurations",
                column: "config_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alert_configurations_updated_at",
                table: "alert_configurations",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_alert_type",
                table: "alert_rules",
                column: "alert_type");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_created_at",
                table: "alert_rules",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_is_enabled",
                table: "alert_rules",
                column: "is_enabled");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_name",
                table: "alert_rules",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alerts_alert_type_triggered_at",
                table: "alerts",
                columns: new[] { "alert_type", "triggered_at" });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_is_active",
                table: "alerts",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_feedback_AnalysisId",
                table: "analysis_feedback",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_feedback_IsExported",
                table: "analysis_feedback",
                column: "IsExported");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_feedback_IsReviewed",
                table: "analysis_feedback",
                column: "IsReviewed");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_feedback_UserId_AnalysisId",
                table: "analysis_feedback",
                columns: new[] { "UserId", "AnalysisId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_key_usage_logs_key_id",
                table: "api_key_usage_logs",
                column: "key_id");

            migrationBuilder.CreateIndex(
                name: "ix_api_key_usage_logs_key_id_used_at",
                table: "api_key_usage_logs",
                columns: new[] { "key_id", "used_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_api_key_usage_logs_used_at",
                table: "api_key_usage_logs",
                column: "used_at");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_IsActive_ExpiresAt",
                table: "api_keys",
                columns: new[] { "IsActive", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_KeyHash",
                table: "api_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_RevokedBy",
                table: "api_keys",
                column: "RevokedBy");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_UserId",
                table: "api_keys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_accuracy_submitted_at",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                columns: new[] { "accuracy", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_game_session_id",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                column: "game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_had_conflicts",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                column: "had_conflicts");

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_submitted_at",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_user_id",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_validation_id",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                column: "validation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_badges_category",
                table: "badges",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_badges_code_unique",
                table: "badges",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_badges_display_order",
                table: "badges",
                column: "display_order");

            migrationBuilder.CreateIndex(
                name: "ix_badges_is_active",
                table: "badges",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_badges_tier",
                table: "badges",
                column: "tier");

            migrationBuilder.CreateIndex(
                name: "ix_batch_jobs_created_at",
                schema: "administration",
                table: "batch_jobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_batch_jobs_status",
                schema: "administration",
                table: "batch_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_batch_jobs_status_created_at",
                schema: "administration",
                table: "batch_jobs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cache_stats_game_id",
                table: "cache_stats",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_cache_stats_question_hash",
                table: "cache_stats",
                column: "question_hash");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_SessionDeckId",
                schema: "session_tracking",
                table: "Cards",
                column: "SessionDeckId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_SortOrder",
                schema: "session_tracking",
                table: "Cards",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "idx_chat_logs_chat_id_sequence_role",
                table: "chat_logs",
                columns: new[] { "ChatId", "SequenceNumber", "Level" });

            migrationBuilder.CreateIndex(
                name: "idx_chat_logs_deleted_at",
                table: "chat_logs",
                column: "DeletedAt",
                filter: "\"DeletedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_chat_logs_user_id",
                table: "chat_logs",
                column: "UserId",
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_ChatId_CreatedAt",
                table: "chat_logs",
                columns: new[] { "ChatId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_DeletedByUserId",
                table: "chat_logs",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_agent_id",
                table: "chat_sessions",
                column: "agent_id",
                filter: "agent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_agent_session_id",
                table: "chat_sessions",
                column: "agent_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_game_id",
                table: "chat_sessions",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_last_message_at",
                table: "chat_sessions",
                column: "last_message_at");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_user_agent_id",
                table: "chat_sessions",
                columns: new[] { "user_id", "agent_id" },
                filter: "agent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_user_game",
                table: "chat_sessions",
                columns: new[] { "user_id", "game_id" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_user_id",
                table: "chat_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_thread_collections_ChatThreadId",
                table: "chat_thread_collections",
                column: "ChatThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_thread_collections_ChatThreadId_CollectionId",
                table: "chat_thread_collections",
                columns: new[] { "ChatThreadId", "CollectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_thread_collections_CollectionId",
                table: "chat_thread_collections",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_chats_AgentId",
                table: "chats",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_chats_GameId_StartedAt",
                table: "chats",
                columns: new[] { "GameId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_chats_UserId_LastMessageAt",
                table: "chats",
                columns: new[] { "UserId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_GameId",
                table: "ChatThreads",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_UserId",
                table: "ChatThreads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_chunked_upload_sessions_ExpiresAt",
                table: "chunked_upload_sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_chunked_upload_sessions_GameId",
                table: "chunked_upload_sessions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_chunked_upload_sessions_Status",
                table: "chunked_upload_sessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_chunked_upload_sessions_UserId",
                table: "chunked_upload_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_chunked_upload_sessions_UserId_Status",
                table: "chunked_upload_sessions",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_contribution_records_contributor_id",
                table: "contribution_records",
                column: "contributor_id");

            migrationBuilder.CreateIndex(
                name: "ix_contribution_records_contributor_version",
                table: "contribution_records",
                columns: new[] { "contributor_id", "version" });

            migrationBuilder.CreateIndex(
                name: "ix_contribution_records_share_request_id",
                table: "contribution_records",
                column: "share_request_id",
                filter: "share_request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_contributors_shared_game_id",
                table: "contributors",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_contributors_user_id",
                table: "contributors",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_contributors_user_shared_game_unique",
                table: "contributors",
                columns: new[] { "user_id", "shared_game_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversation_memory_session_id",
                table: "conversation_memory",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_memory_timestamp",
                table: "conversation_memory",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_memory_user_id",
                table: "conversation_memory",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_memory_user_id_game_id",
                table: "conversation_memory",
                columns: new[] { "user_id", "game_id" });

            migrationBuilder.CreateIndex(
                name: "IX_CostScenarios_CreatedAt",
                table: "cost_scenarios",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CostScenarios_CreatedByUserId",
                table: "cost_scenarios",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_custom_rag_pipelines_created_by",
                table: "custom_rag_pipelines",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_custom_rag_pipelines_is_published",
                table: "custom_rag_pipelines",
                column: "is_published");

            migrationBuilder.CreateIndex(
                name: "ix_custom_rag_pipelines_is_template",
                table: "custom_rag_pipelines",
                column: "is_template");

            migrationBuilder.CreateIndex(
                name: "ix_custom_rag_pipelines_tags",
                table: "custom_rag_pipelines",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_analysis_depth",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "analysis_depth");

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_game_session_id",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "game_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_outcome_suggestion_followed",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                columns: new[] { "outcome", "suggestion_followed" });

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_quality_submitted_at",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                columns: new[] { "quality", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_submitted_at",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_suggestion_followed",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "suggestion_followed");

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_suggestion_id",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "suggestion_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_user_id",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_collections_CreatedByUserId_CreatedAt",
                table: "document_collections",
                columns: new[] { "CreatedByUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_collections_GameId",
                table: "document_collections",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_correlation_id",
                table: "email_queue_items",
                column: "correlation_id",
                filter: "correlation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_status",
                table: "email_queue_items",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_status_next_retry_at",
                table: "email_queue_items",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_user_id_created_at",
                table: "email_queue_items",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_email_verifications_ExpiresAt",
                table: "email_verifications",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_email_verifications_TokenHash",
                table: "email_verifications",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_verifications_UserId",
                table: "email_verifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_entity_links_owner",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "owner_user_id", "is_deleted" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_entity_links_source",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "source_entity_type", "source_entity_id", "is_deleted" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_entity_links_target",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "target_entity_type", "target_entity_id", "is_deleted" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_entity_links_source_target_type",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "source_entity_type", "source_entity_id", "target_entity_type", "target_entity_id", "link_type" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_extracted_facts_GameId",
                table: "extracted_facts",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_extracted_facts_GameId_FactType",
                table: "extracted_facts",
                columns: new[] { "GameId", "FactType" });

            migrationBuilder.CreateIndex(
                name: "IX_extracted_facts_IsReviewed",
                table: "extracted_facts",
                column: "IsReviewed");

            migrationBuilder.CreateIndex(
                name: "IX_extracted_facts_SourceDocumentId",
                table: "extracted_facts",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "ix_game_analytics_events_game_id_timestamp",
                table: "game_analytics_events",
                columns: new[] { "game_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_game_analytics_events_timestamp",
                table: "game_analytics_events",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_game_categories_name",
                table: "game_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_categories_slug",
                table: "game_categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_checklists_entry_display_order",
                table: "game_checklists",
                columns: new[] { "UserLibraryEntryId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "ix_game_checklists_user_library_entry_id",
                table: "game_checklists",
                column: "UserLibraryEntryId");

            migrationBuilder.CreateIndex(
                name: "ix_game_designers_name",
                table: "game_designers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_errata_published_date",
                table: "game_errata",
                column: "published_date",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_game_errata_shared_game_id",
                table: "game_errata",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_faqs_display_order",
                table: "game_faqs",
                columns: new[] { "shared_game_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_game_faqs_shared_game_id",
                table: "game_faqs",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_GameLabels_IsPredefined",
                table: "game_labels",
                column: "is_predefined");

            migrationBuilder.CreateIndex(
                name: "IX_GameLabels_Name_Predefined",
                table: "game_labels",
                column: "Name",
                unique: true,
                filter: "is_predefined = true");

            migrationBuilder.CreateIndex(
                name: "IX_GameLabels_UserId",
                table: "game_labels",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameLabels_UserId_Name",
                table: "game_labels",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "is_predefined = false");

            migrationBuilder.CreateIndex(
                name: "ix_game_mechanics_name",
                table: "game_mechanics",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_mechanics_slug",
                table: "game_mechanics",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_night_events_organizer_scheduled",
                table: "game_night_events",
                columns: new[] { "organizer_id", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "IX_game_night_events_status_scheduled",
                table: "game_night_events",
                columns: new[] { "status", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_game_night_playlists_creator_user_id",
                table: "game_night_playlists",
                column: "creator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_night_playlists_share_token",
                table: "game_night_playlists",
                column: "share_token",
                unique: true,
                filter: "share_token IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_game_night_rsvps_event_user",
                table: "game_night_rsvps",
                columns: new[] { "event_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_night_rsvps_user_id",
                table: "game_night_rsvps",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_publishers_name",
                table: "game_publishers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_SharedGameId",
                table: "game_reviews",
                column: "SharedGameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_SharedGameId_UserId",
                table: "game_reviews",
                columns: new[] { "SharedGameId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_session_states_game_session_id",
                table: "game_session_states",
                column: "game_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_session_states_last_updated_at",
                table: "game_session_states",
                column: "last_updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_game_session_states_template_id",
                table: "game_session_states",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_entry_played",
                table: "game_sessions",
                columns: new[] { "UserLibraryEntryId", "PlayedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_played_at",
                table: "game_sessions",
                column: "PlayedAt");

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_user_library_entry_id",
                table: "game_sessions",
                column: "UserLibraryEntryId");

            migrationBuilder.CreateIndex(
                name: "ix_game_state_snapshots_created_at",
                table: "game_state_snapshots",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_game_state_snapshots_session_state_id",
                table: "game_state_snapshots",
                column: "session_state_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_state_snapshots_session_state_id_turn_number",
                table: "game_state_snapshots",
                columns: new[] { "session_state_id", "turn_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_state_templates_shared_game_id",
                table: "game_state_templates",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_state_templates_shared_game_id_is_active",
                table: "game_state_templates",
                columns: new[] { "shared_game_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_game_state_templates_shared_game_id_version",
                table: "game_state_templates",
                columns: new[] { "shared_game_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameStrategies_SharedGameId",
                table: "game_strategies",
                column: "SharedGameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameStrategies_SharedGameId_Upvotes",
                table: "game_strategies",
                columns: new[] { "SharedGameId", "Upvotes" });

            migrationBuilder.CreateIndex(
                name: "IX_games_Name",
                table: "games",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_SharedGameId",
                table: "games",
                column: "SharedGameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_CreatedByUserId_Status",
                table: "GameSessions",
                columns: new[] { "CreatedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_GameId",
                table: "GameSessions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_StartedAt",
                table: "GameSessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_Status_StartedAt",
                table: "GameSessions",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_GameId",
                table: "GameToolkits",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_GameId_Version",
                table: "GameToolkits",
                columns: new[] { "GameId", "Version" },
                unique: true,
                filter: "\"GameId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_IsPublished",
                table: "GameToolkits",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_PrivateGameId",
                table: "GameToolkits",
                column: "PrivateGameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_PrivateGameId_Version",
                table: "GameToolkits",
                columns: new[] { "PrivateGameId", "Version" },
                unique: true,
                filter: "\"PrivateGameId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_insight_feedback_insight_type",
                schema: "administration",
                table: "insight_feedback",
                column: "insight_type");

            migrationBuilder.CreateIndex(
                name: "IX_insight_feedback_insight_type_is_relevant",
                schema: "administration",
                table: "insight_feedback",
                columns: new[] { "insight_type", "is_relevant" });

            migrationBuilder.CreateIndex(
                name: "IX_insight_feedback_submitted_at",
                schema: "administration",
                table: "insight_feedback",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "IX_insight_feedback_user_id",
                schema: "administration",
                table: "insight_feedback",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_insight_feedback_user_id_insight_id",
                schema: "administration",
                table: "insight_feedback",
                columns: new[] { "user_id", "insight_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_accepted_by_user_id",
                table: "invitation_tokens",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_email_status",
                table: "invitation_tokens",
                columns: new[] { "email", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_expires_at",
                table: "invitation_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_invited_by_user_id",
                table: "invitation_tokens",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_token_hash",
                table: "invitation_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_Category",
                table: "ledger_entries",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_CreatedByUserId",
                table: "ledger_entries",
                column: "CreatedByUserId",
                filter: "\"CreatedByUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_Date",
                table: "ledger_entries",
                column: "Date",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_Date_Type",
                table: "ledger_entries",
                columns: new[] { "Date", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_Source",
                table: "ledger_entries",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_Type",
                table: "ledger_entries",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "ix_library_share_links_expires_at",
                table: "library_share_links",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_library_share_links_privacy_level",
                table: "library_share_links",
                column: "privacy_level");

            migrationBuilder.CreateIndex(
                name: "ix_library_share_links_revoked_at",
                table: "library_share_links",
                column: "revoked_at");

            migrationBuilder.CreateIndex(
                name: "ix_library_share_links_share_token",
                table: "library_share_links",
                column: "share_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_library_share_links_user_id",
                table: "library_share_links",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_created_at",
                table: "live_game_sessions",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_created_by_user_id",
                table: "live_game_sessions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_game_status",
                table: "live_game_sessions",
                columns: new[] { "game_id", "status" },
                filter: "game_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_session_code",
                table: "live_game_sessions",
                column: "session_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_status",
                table: "live_game_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_user_status",
                table: "live_game_sessions",
                columns: new[] { "created_by_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_live_round_scores_session_id",
                table: "live_session_round_scores",
                column: "live_game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_round_scores_session_player_round_dim",
                table: "live_session_round_scores",
                columns: new[] { "live_game_session_id", "player_id", "round", "dimension" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_round_scores_player_id",
                table: "live_session_round_scores",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_live_session_turn_records_player_id",
                table: "live_session_turn_records",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_turn_records_session_id",
                table: "live_session_turn_records",
                column: "live_game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_turn_records_session_turn",
                table: "live_session_turn_records",
                columns: new[] { "live_game_session_id", "turn_index" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_cost_logs_created_at",
                table: "llm_cost_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_llm_cost_logs_provider_date",
                table: "llm_cost_logs",
                columns: new[] { "provider", "request_date" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_cost_logs_request_date",
                table: "llm_cost_logs",
                column: "request_date");

            migrationBuilder.CreateIndex(
                name: "ix_llm_cost_logs_role_date",
                table: "llm_cost_logs",
                columns: new[] { "user_role", "request_date" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_cost_logs_user_id",
                table: "llm_cost_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_llm_request_logs_expires_at",
                table: "llm_request_logs",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_llm_request_logs_model_requested_at",
                table: "llm_request_logs",
                columns: new[] { "model_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_request_logs_provider",
                table: "llm_request_logs",
                column: "provider");

            migrationBuilder.CreateIndex(
                name: "ix_llm_request_logs_requested_at_source",
                table: "llm_request_logs",
                columns: new[] { "requested_at", "request_source" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_request_logs_user_requested_at",
                table: "llm_request_logs",
                columns: new[] { "user_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_drafts_game_pdf_status",
                table: "mechanic_drafts",
                columns: new[] { "shared_game_id", "pdf_document_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_drafts_pdf_document_id",
                table: "mechanic_drafts",
                column: "pdf_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_drafts_shared_game_id",
                table: "mechanic_drafts",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_model_change_logs_AffectedStrategy",
                table: "model_change_logs",
                column: "AffectedStrategy");

            migrationBuilder.CreateIndex(
                name: "IX_model_change_logs_ChangeType",
                table: "model_change_logs",
                column: "ChangeType");

            migrationBuilder.CreateIndex(
                name: "IX_model_change_logs_ModelId",
                table: "model_change_logs",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_model_change_logs_OccurredAt",
                table: "model_change_logs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_model_compatibility_entries_IsCurrentlyAvailable",
                table: "model_compatibility_entries",
                column: "IsCurrentlyAvailable");

            migrationBuilder.CreateIndex(
                name: "IX_model_compatibility_entries_IsDeprecated",
                table: "model_compatibility_entries",
                column: "IsDeprecated");

            migrationBuilder.CreateIndex(
                name: "IX_model_compatibility_entries_ModelId",
                table: "model_compatibility_entries",
                column: "ModelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model_compatibility_entries_Provider",
                table: "model_compatibility_entries",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_n8n_configs_CreatedByUserId",
                table: "n8n_configs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_n8n_configs_Name",
                table: "n8n_configs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_UserId",
                table: "notification_preferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_queue_items_channel_type_status",
                table: "notification_queue_items",
                columns: new[] { "channel_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_queue_items_correlation_id",
                table: "notification_queue_items",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_queue_items_recipient_user_id_created_at",
                table: "notification_queue_items",
                columns: new[] { "recipient_user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_notification_queue_items_status_next_retry_at",
                table: "notification_queue_items",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_correlation_id",
                table: "notifications",
                column: "correlation_id",
                filter: "correlation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_created_at",
                table: "notifications",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_is_read",
                table: "notifications",
                columns: new[] { "user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_is_read_created_at",
                table: "notifications",
                columns: new[] { "user_id", "is_read", "created_at" },
                filter: "is_read = false");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_accounts_Provider",
                table: "oauth_accounts",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_accounts_Provider_ProviderUserId",
                table: "oauth_accounts",
                columns: new[] { "Provider", "ProviderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_oauth_accounts_UserId",
                table: "oauth_accounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_ExpiresAt",
                table: "password_reset_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_TokenHash",
                table: "password_reset_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_UserId",
                table: "password_reset_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_base_document_id",
                table: "pdf_documents",
                column: "base_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_CollectionId",
                table: "pdf_documents",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_CollectionId_SortOrder",
                table: "pdf_documents",
                columns: new[] { "CollectionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_content_hash_game_id",
                table: "pdf_documents",
                columns: new[] { "content_hash", "GameId" });

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_content_hash_private_game_id",
                table: "pdf_documents",
                columns: new[] { "content_hash", "PrivateGameId" });

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_document_category",
                table: "pdf_documents",
                column: "document_category");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_GameId_UploadedAt",
                table: "pdf_documents",
                columns: new[] { "GameId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_is_active_for_rag",
                table: "pdf_documents",
                column: "is_active_for_rag");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_PrivateGameId",
                table: "pdf_documents",
                column: "PrivateGameId");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_UploadedByUserId",
                table: "pdf_documents",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_processing_metrics_pdf_document_id",
                table: "pdf_processing_metrics",
                column: "pdf_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_processing_metrics_step_created_at",
                table: "pdf_processing_metrics",
                columns: new[] { "step", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PlayRecords_CreatedByUserId",
                table: "play_records",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayRecords_GameId",
                table: "play_records",
                column: "GameId",
                filter: "\"GameId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlayRecords_SessionDate",
                table: "play_records",
                column: "SessionDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_PlayRecords_Status",
                table: "play_records",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_agent_definition_id",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "agent_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_category",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_created_at",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_created_by",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_is_active",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_name",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_private_games_agent_definition_id",
                table: "private_games",
                column: "agent_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_private_games_owner_bgg",
                table: "private_games",
                columns: new[] { "owner_id", "bgg_id" },
                unique: true,
                filter: "bgg_id IS NOT NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_private_games_owner_id",
                table: "private_games",
                column: "owner_id",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_private_games_title",
                table: "private_games",
                column: "title",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_processing_jobs_pdf_document",
                table: "processing_jobs",
                column: "pdf_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_processing_jobs_status",
                table: "processing_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_processing_jobs_status_priority_created",
                table: "processing_jobs",
                columns: new[] { "status", "priority", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_processing_jobs_user_status",
                table: "processing_jobs",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_processing_steps_job_id",
                table: "processing_steps",
                column: "processing_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_audit_logs_Action",
                table: "prompt_audit_logs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_audit_logs_ChangedAt",
                table: "prompt_audit_logs",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_audit_logs_ChangedByUserId",
                table: "prompt_audit_logs",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_audit_logs_TemplateId",
                table: "prompt_audit_logs",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_audit_logs_VersionId",
                table: "prompt_audit_logs",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_evaluation_results_executed_at",
                table: "prompt_evaluation_results",
                column: "executed_at");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_evaluation_results_template_id",
                table: "prompt_evaluation_results",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_evaluation_results_template_id_version_id_executed_at",
                table: "prompt_evaluation_results",
                columns: new[] { "template_id", "version_id", "executed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_prompt_evaluation_results_version_id",
                table: "prompt_evaluation_results",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_templates_Category",
                table: "prompt_templates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_templates_CreatedAt",
                table: "prompt_templates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_templates_CreatedByUserId",
                table: "prompt_templates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_templates_Name",
                table: "prompt_templates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompt_versions_CreatedAt",
                table: "prompt_versions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_versions_CreatedByUserId",
                table: "prompt_versions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_versions_TemplateId_IsActive",
                table: "prompt_versions",
                columns: new[] { "TemplateId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_prompt_versions_TemplateId_VersionNumber",
                table: "prompt_versions",
                columns: new[] { "TemplateId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalMigrations_PrivateGameId",
                table: "ProposalMigrations",
                column: "PrivateGameId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalMigrations_SharedGameId",
                table: "ProposalMigrations",
                column: "SharedGameId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalMigrations_ShareRequestId",
                table: "ProposalMigrations",
                column: "ShareRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalMigrations_UserId_Choice",
                table: "ProposalMigrations",
                columns: new[] { "UserId", "Choice" });

            migrationBuilder.CreateIndex(
                name: "ix_quick_questions_active",
                table: "quick_questions",
                columns: new[] { "shared_game_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_quick_questions_order",
                table: "quick_questions",
                columns: new[] { "shared_game_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_quick_questions_shared_game_id",
                table: "quick_questions",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_executed_by_user_id",
                table: "rag_executions",
                column: "executed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_parent_execution_id",
                table: "rag_executions",
                column: "parent_execution_id",
                filter: "parent_execution_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_strategy_executed_at",
                table: "rag_executions",
                columns: new[] { "strategy_id", "executed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_strategy_id",
                table: "rag_executions",
                column: "strategy_id");

            migrationBuilder.CreateIndex(
                name: "IX_rag_executions_agent_definition_id",
                schema: "knowledge_base",
                table: "rag_executions",
                column: "agent_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_rag_executions_created_at",
                schema: "knowledge_base",
                table: "rag_executions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_rag_executions_status",
                schema: "knowledge_base",
                table: "rag_executions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_rag_executions_strategy",
                schema: "knowledge_base",
                table: "rag_executions",
                column: "strategy");

            migrationBuilder.CreateIndex(
                name: "ix_rag_pipeline_strategies_created_by_user_id",
                table: "rag_pipeline_strategies",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rag_pipeline_strategies_is_template",
                table: "rag_pipeline_strategies",
                column: "is_template",
                filter: "is_template = true");

            migrationBuilder.CreateIndex(
                name: "ix_rag_pipeline_strategies_name_user",
                table: "rag_pipeline_strategies",
                columns: new[] { "name", "created_by_user_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_rag_user_configs_UserId",
                table: "rag_user_configs",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordPlayers_PlayRecordId",
                table: "record_players",
                column: "PlayRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordPlayers_UserId",
                table: "record_players",
                column: "UserId",
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecordScores_Player_Dimension_Unique",
                table: "record_scores",
                columns: new[] { "RecordPlayerId", "Dimension" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceForecasts_CreatedAt",
                table: "resource_forecasts",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceForecasts_CreatedByUserId",
                table: "resource_forecasts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_rule_atoms_RuleSpecId_SortOrder",
                table: "rule_atoms",
                columns: new[] { "RuleSpecId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RuleConflictFAQs_GameId_Pattern",
                table: "rule_conflict_faqs",
                columns: new[] { "GameId", "Pattern" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleConflictFAQs_UsageCount",
                table: "rule_conflict_faqs",
                column: "UsageCount");

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_CreatedByUserId",
                table: "rule_specs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_GameEntityId",
                table: "rule_specs",
                column: "GameEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_GameId_Version",
                table: "rule_specs",
                columns: new[] { "GameId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_ParentVersionId",
                table: "rule_specs",
                column: "ParentVersionId");

            migrationBuilder.CreateIndex(
                name: "ix_rulebook_analyses_game_pdf_active",
                table: "rulebook_analyses",
                columns: new[] { "shared_game_id", "pdf_document_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_rulebook_analyses_game_pdf_version",
                table: "rulebook_analyses",
                columns: new[] { "shared_game_id", "pdf_document_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rulebook_analyses_pdf_document_id",
                table: "rulebook_analyses",
                column: "pdf_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_rulebook_analyses_shared_game_id",
                table: "rulebook_analyses",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "idx_rulespec_comments_game_version_line",
                table: "rulespec_comments",
                columns: new[] { "GameId", "Version", "LineNumber" });

            migrationBuilder.CreateIndex(
                name: "idx_rulespec_comments_is_resolved",
                table: "rulespec_comments",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "idx_rulespec_comments_parent_id",
                table: "rulespec_comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "idx_rulespec_comments_user_id",
                table: "rulespec_comments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_rulespec_comments_AtomId",
                table: "rulespec_comments",
                column: "AtomId");

            migrationBuilder.CreateIndex(
                name: "IX_rulespec_comments_GameId_Version",
                table: "rulespec_comments",
                columns: new[] { "GameId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_rulespec_comments_ResolvedByUserId",
                table: "rulespec_comments",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_session_attachment_cleanup",
                table: "session_attachments",
                column: "created_at",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_session_attachment_player_id",
                table: "session_attachments",
                column: "player_id",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_session_attachment_session_id",
                table: "session_attachments",
                column: "session_id",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_session_attachment_snapshot",
                table: "session_attachments",
                columns: new[] { "session_id", "snapshot_index" },
                filter: "is_deleted = false AND snapshot_index IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_session_checkpoints_CreatedBy",
                table: "session_checkpoints",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_session_checkpoints_IsDeleted",
                table: "session_checkpoints",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_session_checkpoints_SessionId_Timestamp",
                table: "session_checkpoints",
                columns: new[] { "SessionId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_session_events_CreatedBy",
                table: "session_events",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_session_events_SessionId_EventType",
                table: "session_events",
                columns: new[] { "SessionId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_session_events_SessionId_Timestamp",
                table: "session_events",
                columns: new[] { "SessionId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_session_invites_link_token",
                table: "session_invites",
                column: "link_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_invites_pin",
                table: "session_invites",
                column: "pin");

            migrationBuilder.CreateIndex(
                name: "ix_session_invites_session_id",
                table: "session_invites",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_participants_connection_token",
                table: "session_participants",
                column: "connection_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_participants_session_id",
                table: "session_participants",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_participants_user_id",
                table: "session_participants",
                column: "user_id",
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_session_players_session_id",
                table: "session_players",
                column: "live_game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_players_session_user",
                table: "session_players",
                columns: new[] { "live_game_session_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_session_players_team_id",
                table: "session_players",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_players_user_id",
                table: "session_players",
                column: "user_id",
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_session_snapshots_session_id",
                table: "session_snapshots",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_snapshots_session_id_is_checkpoint",
                table: "session_snapshots",
                columns: new[] { "session_id", "is_checkpoint" });

            migrationBuilder.CreateIndex(
                name: "ix_session_snapshots_session_id_snapshot_index",
                table: "session_snapshots",
                columns: new[] { "session_id", "snapshot_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_snapshots_timestamp",
                table: "session_snapshots",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_session_teams_session_id",
                table: "session_teams",
                column: "live_game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_teams_session_name",
                table: "session_teams",
                columns: new[] { "live_game_session_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_cards_session",
                table: "session_tracking_card_draws",
                columns: new[] { "session_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_card_draws_participant_id",
                table: "session_tracking_card_draws",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_chat_messages_sender_id",
                table: "session_tracking_chat_messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_chat_messages_session_id",
                table: "session_tracking_chat_messages",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_chat_messages_session_id_sequence_number",
                table: "session_tracking_chat_messages",
                columns: new[] { "session_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_dice_session_timestamp",
                table: "session_tracking_dice_rolls",
                columns: new[] { "session_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_dice_rolls_participant_id",
                table: "session_tracking_dice_rolls",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_media_participant_id",
                table: "session_tracking_media",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_media_session_id",
                table: "session_tracking_media",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_media_session_id_created_at",
                table: "session_tracking_media",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_media_snapshot_id",
                table: "session_tracking_media",
                column: "snapshot_id");

            migrationBuilder.CreateIndex(
                name: "idx_notes_session_participant",
                table: "session_tracking_notes",
                columns: new[] { "session_id", "participant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_notes_participant_id",
                table: "session_tracking_notes",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "idx_participants_session",
                table: "session_tracking_participants",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_participants_user_id",
                table: "session_tracking_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_scores_round",
                table: "session_tracking_score_entries",
                columns: new[] { "session_id", "round_number" });

            migrationBuilder.CreateIndex(
                name: "idx_scores_session_participant",
                table: "session_tracking_score_entries",
                columns: new[] { "session_id", "participant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_score_entries_participant_id",
                table: "session_tracking_score_entries",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_code",
                table: "session_tracking_sessions",
                column: "session_code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_game_date",
                table: "session_tracking_sessions",
                columns: new[] { "game_id", "session_date" },
                descending: new[] { false, true },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_user_status",
                table: "session_tracking_sessions",
                columns: new[] { "user_id", "status" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_SessionDecks_IsDeleted",
                schema: "session_tracking",
                table: "SessionDecks",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SessionDecks_SessionId",
                schema: "session_tracking",
                table: "SessionDecks",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionNotes_IsDeleted",
                table: "SessionNotes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SessionNotes_ParticipantId",
                table: "SessionNotes",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionNotes_SessionId",
                table: "SessionNotes",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionNotes_SessionId_ParticipantId",
                table: "SessionNotes",
                columns: new[] { "SessionId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "ix_share_links_creator_id",
                table: "share_links",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_expires_at",
                table: "share_links",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_revoked_at",
                table: "share_links",
                column: "revoked_at");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_thread_id",
                table: "share_links",
                column: "thread_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_request_documents_document_id",
                table: "share_request_documents",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_request_documents_request_document",
                table: "share_request_documents",
                columns: new[] { "share_request_id", "document_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_share_request_documents_share_request_id",
                table: "share_request_documents",
                column: "share_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_request_limit_configs_is_active",
                table: "share_request_limit_configs",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_share_request_limit_configs_tier_unique_active",
                table: "share_request_limit_configs",
                column: "tier",
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_review_lock_expires_at",
                table: "share_requests",
                column: "review_lock_expires_at",
                filter: "review_lock_expires_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_reviewing_admin_id",
                table: "share_requests",
                column: "reviewing_admin_id",
                filter: "reviewing_admin_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_source_game_id",
                table: "share_requests",
                column: "source_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_source_private_game_id",
                table: "share_requests",
                column: "source_private_game_id",
                filter: "source_private_game_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_status",
                table: "share_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_share_requests_target_shared_game_id",
                table: "share_requests",
                column: "target_shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_user_id",
                table: "share_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_user_source_status",
                table: "share_requests",
                columns: new[] { "user_id", "source_game_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_shared_game_categories_shared_game_id",
                table: "shared_game_categories",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_delete_requests_created_at",
                table: "shared_game_delete_requests",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_delete_requests_shared_game_id",
                table: "shared_game_delete_requests",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_delete_requests_status",
                table: "shared_game_delete_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_shared_game_designers_shared_game_id",
                table: "shared_game_designers",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_shared_game_documents_active_version",
                table: "shared_game_documents",
                columns: new[] { "shared_game_id", "document_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_shared_game_documents_pdf_document_id",
                table: "shared_game_documents",
                column: "pdf_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_shared_game_documents_shared_game_id",
                table: "shared_game_documents",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_shared_game_documents_version_unique",
                table: "shared_game_documents",
                columns: new[] { "shared_game_id", "document_type", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_game_mechanics_shared_game_id",
                table: "shared_game_mechanics",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_shared_game_publishers_shared_game_id",
                table: "shared_game_publishers",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_shared_games_agent_definition_id",
                table: "shared_games",
                column: "agent_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_shared_games_bgg_id",
                table: "shared_games",
                column: "bgg_id",
                unique: true,
                filter: "bgg_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_shared_games_status",
                table: "shared_games",
                column: "status",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_shared_games_title",
                table: "shared_games",
                column: "title",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_similarity_audit_results_PairId",
                table: "similarity_audit_results",
                column: "PairId");

            migrationBuilder.CreateIndex(
                name: "IX_similarity_audit_results_PairId_CheckName",
                table: "similarity_audit_results",
                columns: new[] { "PairId", "CheckName" });

            migrationBuilder.CreateIndex(
                name: "IX_similarity_audit_results_Passed",
                table: "similarity_audit_results",
                column: "Passed");

            migrationBuilder.CreateIndex(
                name: "IX_similarity_audit_results_SourceGame",
                table: "similarity_audit_results",
                column: "SourceGame");

            migrationBuilder.CreateIndex(
                name: "IX_slack_connections_is_active",
                table: "slack_connections",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_slack_connections_slack_user_id",
                table: "slack_connections",
                column: "slack_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_slack_connections_user_id_slack_team_id",
                table: "slack_connections",
                columns: new[] { "user_id", "slack_team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slack_team_channel_configs_channel_name",
                table: "slack_team_channel_configs",
                column: "channel_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slack_team_channel_configs_is_enabled",
                table: "slack_team_channel_configs",
                column: "is_enabled",
                filter: "is_enabled = true");

            migrationBuilder.CreateIndex(
                name: "ix_step_log_entries_step_id",
                table: "step_log_entries",
                column: "processing_step_id");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_model_mapping_AdminOnly",
                table: "strategy_model_mapping",
                column: "AdminOnly");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_model_mapping_Provider",
                table: "strategy_model_mapping",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_model_mapping_Strategy",
                table: "strategy_model_mapping",
                column: "Strategy",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_strategy_patterns_evaluation_score",
                table: "strategy_patterns",
                column: "evaluation_score");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_patterns_game_id",
                table: "strategy_patterns",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_patterns_game_id_applicable_phase",
                table: "strategy_patterns",
                columns: new[] { "game_id", "applicable_phase" });

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_Category",
                table: "system_configurations",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_CreatedByUserId",
                table: "system_configurations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_Environment",
                table: "system_configurations",
                column: "Environment");

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_IsActive",
                table: "system_configurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_Key_Environment",
                table: "system_configurations",
                columns: new[] { "Key", "Environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_UpdatedAt",
                table: "system_configurations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_UpdatedByUserId",
                table: "system_configurations",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_temp_sessions_ExpiresAt",
                table: "temp_sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_temp_sessions_TokenHash",
                table: "temp_sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_temp_sessions_UserId",
                table: "temp_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_ChunkIndex",
                table: "text_chunks",
                column: "ChunkIndex");

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_GameId",
                table: "text_chunks",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_PageNumber",
                table: "text_chunks",
                column: "PageNumber");

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_PdfDocumentId",
                table: "text_chunks",
                column: "PdfDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_tier_definitions_name",
                table: "tier_definitions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tier_strategy_access_IsEnabled",
                table: "tier_strategy_access",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_tier_strategy_access_Strategy",
                table: "tier_strategy_access",
                column: "Strategy");

            migrationBuilder.CreateIndex(
                name: "IX_tier_strategy_access_Tier",
                table: "tier_strategy_access",
                column: "Tier");

            migrationBuilder.CreateIndex(
                name: "IX_tier_strategy_access_Tier_Strategy",
                table: "tier_strategy_access",
                columns: new[] { "Tier", "Strategy" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_token_tiers_is_active",
                table: "token_tiers",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_token_tiers_name",
                table: "token_tiers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tool_states_session_id",
                table: "tool_states",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_tool_states_session_tool_name",
                table: "tool_states",
                columns: new[] { "session_id", "tool_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tool_states_toolkit_id",
                table: "tool_states",
                column: "toolkit_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_phases_toolbox_id",
                schema: "game_toolbox",
                table: "toolbox_phases",
                column: "toolbox_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_templates_game_id",
                schema: "game_toolbox",
                table: "toolbox_templates",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_tools_toolbox_id",
                schema: "game_toolbox",
                table: "toolbox_tools",
                column: "toolbox_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolboxes_game_id",
                schema: "game_toolbox",
                table: "toolboxes",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolboxes_is_deleted",
                schema: "game_toolbox",
                table: "toolboxes",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_toolkit_session_states_session_id",
                schema: "session_tracking",
                table: "toolkit_session_states",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "uq_toolkit_session_states_session_toolkit",
                schema: "session_tracking",
                table: "toolkit_session_states",
                columns: new[] { "session_id", "toolkit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_toolkit_widgets_toolkit_type",
                schema: "game_toolkit",
                table: "toolkit_widgets",
                columns: new[] { "toolkit_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolkits_game_id",
                schema: "game_toolkit",
                table: "toolkits",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "uq_toolkits_game_owner",
                schema: "game_toolkit",
                table: "toolkits",
                columns: new[] { "game_id", "owner_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_turn_orders_session_id",
                table: "turn_orders",
                column: "session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_typology_prompt_templates_current",
                table: "typology_prompt_templates",
                columns: new[] { "typology_id", "is_current" },
                unique: true,
                filter: "is_current = true");

            migrationBuilder.CreateIndex(
                name: "ix_typology_prompt_templates_typology_id",
                table: "typology_prompt_templates",
                column: "typology_id");

            migrationBuilder.CreateIndex(
                name: "ix_typology_prompt_templates_typology_version",
                table: "typology_prompt_templates",
                columns: new[] { "typology_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_used_totp_codes_expiry",
                table: "used_totp_codes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_used_totp_codes_user_code_unique",
                table: "used_totp_codes",
                columns: new[] { "UserId", "CodeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_achievements_AchievementId",
                table: "user_achievements",
                column: "AchievementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId",
                table: "user_achievements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "user_achievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_UnlockedAt",
                table: "user_achievements",
                columns: new[] { "UserId", "UnlockedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_ai_consents_UserId",
                table: "user_ai_consents",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_backup_codes_CodeHash",
                table: "user_backup_codes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_backup_codes_UserId",
                table: "user_backup_codes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_backup_codes_UserId_IsUsed",
                table: "user_backup_codes",
                columns: new[] { "UserId", "IsUsed" },
                filter: "\"IsUsed\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_badge_id",
                table: "user_badges",
                column: "badge_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_revoked_at",
                table: "user_badges",
                column: "revoked_at",
                filter: "revoked_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_triggering_share_request_id",
                table: "user_badges",
                column: "triggering_share_request_id",
                filter: "triggering_share_request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_user_badge_unique",
                table: "user_badges",
                columns: new[] { "user_id", "badge_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_user_id",
                table: "user_badges",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserCollectionEntries_EntityType_EntityId",
                table: "user_collection_entries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCollectionEntries_UserId_EntityType_EntityId",
                table: "user_collection_entries",
                columns: new[] { "UserId", "EntityType", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCollectionEntries_UserId_Favorites",
                table: "user_collection_entries",
                column: "UserId",
                filter: "\"IsFavorite\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UserGameLabels_EntryId_LabelId",
                table: "user_game_labels",
                columns: new[] { "UserLibraryEntryId", "LabelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGameLabels_LabelId",
                table: "user_game_labels",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGameLabels_UserLibraryEntryId",
                table: "user_game_labels",
                column: "UserLibraryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_user_library_entries_private_game_id",
                table: "user_library_entries",
                column: "private_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_library_entries_shared_game_id",
                table: "user_library_entries",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_AddedAt",
                table: "user_library_entries",
                column: "AddedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_CurrentState",
                table: "user_library_entries",
                column: "CurrentState");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_CustomAgentConfigJson",
                table: "user_library_entries",
                column: "CustomAgentConfigJson")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_LastPlayed",
                table: "user_library_entries",
                column: "LastPlayed");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_PrivatePdfId",
                table: "user_library_entries",
                column: "PrivatePdfId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId",
                table: "user_library_entries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId_AddedAt",
                table: "user_library_entries",
                columns: new[] { "UserId", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId_CurrentState",
                table: "user_library_entries",
                columns: new[] { "UserId", "CurrentState" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId_PrivateGameId",
                table: "user_library_entries",
                columns: new[] { "UserId", "private_game_id" },
                unique: true,
                filter: "private_game_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId_SharedGameId",
                table: "user_library_entries",
                columns: new[] { "UserId", "shared_game_id" },
                unique: true,
                filter: "shared_game_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_rate_limit_overrides_created_by_admin_id",
                table: "user_rate_limit_overrides",
                column: "created_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_rate_limit_overrides_expires_at",
                table: "user_rate_limit_overrides",
                column: "expires_at",
                filter: "expires_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_rate_limit_overrides_user_id_active",
                table: "user_rate_limit_overrides",
                column: "user_id",
                unique: true,
                filter: "expires_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_TokenHash",
                table: "user_sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId",
                table: "user_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId_DeviceFingerprint",
                table: "user_sessions",
                columns: new[] { "UserId", "DeviceFingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_user_token_usage_last_reset",
                table: "user_token_usage",
                column: "last_reset");

            migrationBuilder.CreateIndex(
                name: "IX_user_token_usage_tier_id",
                table: "user_token_usage",
                column: "tier_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_token_usage_user_id",
                table: "user_token_usage",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTokenUsage_TierId_IsBlocked",
                table: "user_token_usage",
                columns: new[] { "tier_id", "is_blocked" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTokenUsage_UserId_TokensUsed",
                table: "user_token_usage",
                columns: new[] { "user_id", "tokens_used" });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_accuracy",
                table: "validation_accuracy_baselines",
                column: "accuracy");

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_context",
                table: "validation_accuracy_baselines",
                column: "context");

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_dataset_id",
                table: "validation_accuracy_baselines",
                column: "dataset_id");

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_evaluation_id",
                table: "validation_accuracy_baselines",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_measured_at",
                table: "validation_accuracy_baselines",
                column: "measured_at");

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_meets_baseline",
                table: "validation_accuracy_baselines",
                column: "meets_baseline");

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_GameId",
                table: "vector_documents",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_PdfDocumentId",
                table: "vector_documents",
                column: "PdfDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_shared_game_id",
                table: "vector_documents",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_whiteboard_states_session_id",
                table: "whiteboard_states",
                column: "session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_items_GameId",
                table: "wishlist_items",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_UserId",
                table: "wishlist_items",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_UserId_GameId",
                table: "wishlist_items",
                columns: new[] { "UserId", "GameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_UserId_Priority",
                table: "wishlist_items",
                columns: new[] { "UserId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_error_logs_created_at",
                table: "workflow_error_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_error_logs_execution_id",
                table: "workflow_error_logs",
                column: "execution_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_error_logs_workflow_id",
                table: "workflow_error_logs",
                column: "workflow_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ab_test_variants",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "access_requests");

            migrationBuilder.DropTable(
                name: "admin_rag_strategies",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "admin_report_executions");

            migrationBuilder.DropTable(
                name: "agent_configurations");

            migrationBuilder.DropTable(
                name: "agent_feedback");

            migrationBuilder.DropTable(
                name: "agent_game_state_snapshots");

            migrationBuilder.DropTable(
                name: "agent_test_results");

            migrationBuilder.DropTable(
                name: "ai_request_logs");

            migrationBuilder.DropTable(
                name: "AiModelConfigurations",
                schema: "SystemConfiguration");

            migrationBuilder.DropTable(
                name: "alert_configurations");

            migrationBuilder.DropTable(
                name: "alert_rules");

            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "analysis_feedback");

            migrationBuilder.DropTable(
                name: "api_key_usage_logs");

            migrationBuilder.DropTable(
                name: "arbitro_validation_feedback",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "batch_jobs",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "BggImportQueue");

            migrationBuilder.DropTable(
                name: "cache_stats");

            migrationBuilder.DropTable(
                name: "Cards",
                schema: "session_tracking");

            migrationBuilder.DropTable(
                name: "chat_logs");

            migrationBuilder.DropTable(
                name: "chat_sessions");

            migrationBuilder.DropTable(
                name: "chat_thread_collections");

            migrationBuilder.DropTable(
                name: "chunked_upload_sessions");

            migrationBuilder.DropTable(
                name: "contribution_records");

            migrationBuilder.DropTable(
                name: "conversation_memory");

            migrationBuilder.DropTable(
                name: "cost_scenarios");

            migrationBuilder.DropTable(
                name: "custom_rag_pipelines");

            migrationBuilder.DropTable(
                name: "decisore_move_feedback",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "email_queue_items");

            migrationBuilder.DropTable(
                name: "email_templates");

            migrationBuilder.DropTable(
                name: "email_verifications");

            migrationBuilder.DropTable(
                name: "entity_links",
                schema: "entity_relationships");

            migrationBuilder.DropTable(
                name: "extracted_facts");

            migrationBuilder.DropTable(
                name: "game_analytics_events");

            migrationBuilder.DropTable(
                name: "game_checklists");

            migrationBuilder.DropTable(
                name: "game_errata");

            migrationBuilder.DropTable(
                name: "game_faqs");

            migrationBuilder.DropTable(
                name: "game_night_playlists");

            migrationBuilder.DropTable(
                name: "game_night_rsvps");

            migrationBuilder.DropTable(
                name: "game_reviews");

            migrationBuilder.DropTable(
                name: "game_sessions");

            migrationBuilder.DropTable(
                name: "game_state_snapshots");

            migrationBuilder.DropTable(
                name: "game_state_templates");

            migrationBuilder.DropTable(
                name: "game_strategies");

            migrationBuilder.DropTable(
                name: "GameToolkits");

            migrationBuilder.DropTable(
                name: "insight_feedback",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "library_share_links");

            migrationBuilder.DropTable(
                name: "live_session_round_scores");

            migrationBuilder.DropTable(
                name: "live_session_turn_records");

            migrationBuilder.DropTable(
                name: "llm_cost_logs");

            migrationBuilder.DropTable(
                name: "llm_request_logs");

            migrationBuilder.DropTable(
                name: "LlmSystemConfigs",
                schema: "SystemConfiguration");

            migrationBuilder.DropTable(
                name: "mechanic_drafts");

            migrationBuilder.DropTable(
                name: "model_change_logs");

            migrationBuilder.DropTable(
                name: "model_compatibility_entries");

            migrationBuilder.DropTable(
                name: "n8n_configs");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notification_queue_items");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "oauth_accounts");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "pdf_processing_metrics");

            migrationBuilder.DropTable(
                name: "playground_test_scenarios",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "processing_queue_config");

            migrationBuilder.DropTable(
                name: "prompt_audit_logs");

            migrationBuilder.DropTable(
                name: "prompt_evaluation_results");

            migrationBuilder.DropTable(
                name: "ProposalMigrations");

            migrationBuilder.DropTable(
                name: "quick_questions");

            migrationBuilder.DropTable(
                name: "rag_executions");

            migrationBuilder.DropTable(
                name: "rag_executions",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "rag_pipeline_strategies");

            migrationBuilder.DropTable(
                name: "rag_user_configs");

            migrationBuilder.DropTable(
                name: "record_scores");

            migrationBuilder.DropTable(
                name: "resource_forecasts");

            migrationBuilder.DropTable(
                name: "rule_atoms");

            migrationBuilder.DropTable(
                name: "rule_conflict_faqs");

            migrationBuilder.DropTable(
                name: "rulebook_analyses");

            migrationBuilder.DropTable(
                name: "rulespec_comments");

            migrationBuilder.DropTable(
                name: "session_attachments");

            migrationBuilder.DropTable(
                name: "session_checkpoints");

            migrationBuilder.DropTable(
                name: "session_events");

            migrationBuilder.DropTable(
                name: "session_invites");

            migrationBuilder.DropTable(
                name: "session_participants");

            migrationBuilder.DropTable(
                name: "session_snapshots");

            migrationBuilder.DropTable(
                name: "session_tracking_card_draws");

            migrationBuilder.DropTable(
                name: "session_tracking_chat_messages");

            migrationBuilder.DropTable(
                name: "session_tracking_dice_rolls");

            migrationBuilder.DropTable(
                name: "session_tracking_media");

            migrationBuilder.DropTable(
                name: "session_tracking_notes");

            migrationBuilder.DropTable(
                name: "session_tracking_score_entries");

            migrationBuilder.DropTable(
                name: "SessionNotes");

            migrationBuilder.DropTable(
                name: "share_links");

            migrationBuilder.DropTable(
                name: "share_request_documents");

            migrationBuilder.DropTable(
                name: "share_request_limit_configs");

            migrationBuilder.DropTable(
                name: "shared_game_categories");

            migrationBuilder.DropTable(
                name: "shared_game_delete_requests");

            migrationBuilder.DropTable(
                name: "shared_game_designers");

            migrationBuilder.DropTable(
                name: "shared_game_documents");

            migrationBuilder.DropTable(
                name: "shared_game_mechanics");

            migrationBuilder.DropTable(
                name: "shared_game_publishers");

            migrationBuilder.DropTable(
                name: "similarity_audit_results");

            migrationBuilder.DropTable(
                name: "slack_connections");

            migrationBuilder.DropTable(
                name: "slack_team_channel_configs");

            migrationBuilder.DropTable(
                name: "step_log_entries");

            migrationBuilder.DropTable(
                name: "strategy_model_mapping");

            migrationBuilder.DropTable(
                name: "strategy_patterns");

            migrationBuilder.DropTable(
                name: "system_configurations");

            migrationBuilder.DropTable(
                name: "temp_sessions");

            migrationBuilder.DropTable(
                name: "text_chunks");

            migrationBuilder.DropTable(
                name: "tier_definitions");

            migrationBuilder.DropTable(
                name: "tier_strategy_access");

            migrationBuilder.DropTable(
                name: "token_tiers");

            migrationBuilder.DropTable(
                name: "tool_states");

            migrationBuilder.DropTable(
                name: "toolbox_phases",
                schema: "game_toolbox");

            migrationBuilder.DropTable(
                name: "toolbox_templates",
                schema: "game_toolbox");

            migrationBuilder.DropTable(
                name: "toolbox_tools",
                schema: "game_toolbox");

            migrationBuilder.DropTable(
                name: "toolkit_session_states",
                schema: "session_tracking");

            migrationBuilder.DropTable(
                name: "toolkit_widgets",
                schema: "game_toolkit");

            migrationBuilder.DropTable(
                name: "turn_orders");

            migrationBuilder.DropTable(
                name: "typology_prompt_templates");

            migrationBuilder.DropTable(
                name: "used_totp_codes");

            migrationBuilder.DropTable(
                name: "user_achievements");

            migrationBuilder.DropTable(
                name: "user_ai_consents");

            migrationBuilder.DropTable(
                name: "user_backup_codes");

            migrationBuilder.DropTable(
                name: "user_badges");

            migrationBuilder.DropTable(
                name: "user_collection_entries");

            migrationBuilder.DropTable(
                name: "user_game_labels");

            migrationBuilder.DropTable(
                name: "user_rate_limit_overrides");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "user_token_usage");

            migrationBuilder.DropTable(
                name: "validation_accuracy_baselines");

            migrationBuilder.DropTable(
                name: "vector_documents");

            migrationBuilder.DropTable(
                name: "whiteboard_states");

            migrationBuilder.DropTable(
                name: "wishlist_items");

            migrationBuilder.DropTable(
                name: "workflow_error_logs");

            migrationBuilder.DropTable(
                name: "ab_test_sessions",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "invitation_tokens");

            migrationBuilder.DropTable(
                name: "admin_reports");

            migrationBuilder.DropTable(
                name: "agent_sessions");

            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropTable(
                name: "SessionDecks",
                schema: "session_tracking");

            migrationBuilder.DropTable(
                name: "chats");

            migrationBuilder.DropTable(
                name: "contributors");

            migrationBuilder.DropTable(
                name: "game_night_events");

            migrationBuilder.DropTable(
                name: "game_session_states");

            migrationBuilder.DropTable(
                name: "session_players");

            migrationBuilder.DropTable(
                name: "prompt_versions");

            migrationBuilder.DropTable(
                name: "record_players");

            migrationBuilder.DropTable(
                name: "rule_specs");

            migrationBuilder.DropTable(
                name: "session_tracking_participants");

            migrationBuilder.DropTable(
                name: "ChatThreads");

            migrationBuilder.DropTable(
                name: "share_requests");

            migrationBuilder.DropTable(
                name: "game_categories");

            migrationBuilder.DropTable(
                name: "game_designers");

            migrationBuilder.DropTable(
                name: "game_mechanics");

            migrationBuilder.DropTable(
                name: "game_publishers");

            migrationBuilder.DropTable(
                name: "processing_steps");

            migrationBuilder.DropTable(
                name: "toolboxes",
                schema: "game_toolbox");

            migrationBuilder.DropTable(
                name: "toolkits",
                schema: "game_toolkit");

            migrationBuilder.DropTable(
                name: "achievements");

            migrationBuilder.DropTable(
                name: "badges");

            migrationBuilder.DropTable(
                name: "game_labels");

            migrationBuilder.DropTable(
                name: "user_library_entries");

            migrationBuilder.DropTable(
                name: "agent_typologies");

            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropTable(
                name: "session_teams");

            migrationBuilder.DropTable(
                name: "prompt_templates");

            migrationBuilder.DropTable(
                name: "play_records");

            migrationBuilder.DropTable(
                name: "session_tracking_sessions");

            migrationBuilder.DropTable(
                name: "processing_jobs");

            migrationBuilder.DropTable(
                name: "private_games");

            migrationBuilder.DropTable(
                name: "live_game_sessions");

            migrationBuilder.DropTable(
                name: "pdf_documents");

            migrationBuilder.DropTable(
                name: "document_collections");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "shared_games");

            migrationBuilder.DropTable(
                name: "agent_definitions",
                schema: "knowledge_base");
        }
    }
}
