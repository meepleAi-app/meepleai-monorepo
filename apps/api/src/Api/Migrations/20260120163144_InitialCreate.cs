using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "SystemConfiguration");

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
                name: "agent_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", maxLength: 128, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_feedback", x => x.Id);
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
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
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
                name: "shared_games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bgg_id = table.Column<int>(type: "integer", nullable: true),
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
                    rules_content = table.Column<string>(type: "text", nullable: true),
                    rules_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
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
                    TwoFactorEnabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: true)
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
                    request_date = table.Column<DateOnly>(type: "date", nullable: false)
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
                name: "system_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "user_library_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CustomAgentConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    CustomPdfUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CustomPdfUploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CustomPdfFileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    CustomPdfOriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_library_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_library_entries_shared_games_GameId",
                        column: x => x.GameId,
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
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
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
                    GameEntityId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agents_games_GameEntityId",
                        column: x => x.GameEntityId,
                        principalTable: "games",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MessagesJson = table.Column<string>(type: "text", nullable: false)
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
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
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
                        onDelete: ReferentialAction.Cascade);
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
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PageCount = table.Column<int>(type: "integer", nullable: true),
                    CharacterCount = table.Column<int>(type: "integer", nullable: true),
                    ProcessingError = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExtractedTables = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    ExtractedDiagrams = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    AtomicRules = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    TableCount = table.Column<int>(type: "integer", nullable: true),
                    DiagramCount = table.Column<int>(type: "integer", nullable: true),
                    AtomicRuleCount = table.Column<int>(type: "integer", nullable: true),
                    ProcessingProgressJson = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "base"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false)
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
                        name: "FK_pdf_documents_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
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
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
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
                name: "vector_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    PdfDocumentId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCharacters = table.Column<int>(type: "integer", nullable: false),
                    IndexingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IndexingError = table.Column<string>(type: "text", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmbeddingDimensions = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true)
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
                name: "IX_agents_GameEntityId",
                table: "agents",
                column: "GameEntityId");

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
                column: "Name",
                unique: true);

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
                name: "IX_audit_logs_CreatedAt",
                table: "audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_cache_stats_game_id",
                table: "cache_stats",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_cache_stats_question_hash",
                table: "cache_stats",
                column: "question_hash");

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
                name: "IX_document_collections_CreatedByUserId_CreatedAt",
                table: "document_collections",
                columns: new[] { "CreatedByUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_collections_GameId",
                table: "document_collections",
                column: "GameId");

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
                name: "ix_game_designers_name",
                table: "game_designers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_errata_published_date",
                table: "game_errata",
                column: "published_date",
                descending: Array.Empty<bool>());

            migrationBuilder.CreateIndex(
                name: "ix_game_errata_shared_game_id",
                table: "game_errata",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_faqs_order",
                table: "game_faqs",
                columns: new[] { "shared_game_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_game_faqs_shared_game_id",
                table: "game_faqs",
                column: "shared_game_id");

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
                name: "ix_game_publishers_name",
                table: "game_publishers",
                column: "name",
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
                name: "IX_games_Name",
                table: "games",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_SharedGameId",
                table: "games",
                column: "SharedGameId");

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
                name: "IX_n8n_configs_CreatedByUserId",
                table: "n8n_configs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_n8n_configs_Name",
                table: "n8n_configs",
                column: "Name",
                unique: true);

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
                name: "IX_pdf_documents_CollectionId",
                table: "pdf_documents",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_CollectionId_SortOrder",
                table: "pdf_documents",
                columns: new[] { "CollectionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_GameId_UploadedAt",
                table: "pdf_documents",
                columns: new[] { "GameId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_UploadedByUserId",
                table: "pdf_documents",
                column: "UploadedByUserId");

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
                name: "IX_rule_atoms_RuleSpecId_SortOrder",
                table: "rule_atoms",
                columns: new[] { "RuleSpecId", "SortOrder" });

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
                name: "ix_used_totp_codes_expiry",
                table: "used_totp_codes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_used_totp_codes_user_code_unique",
                table: "used_totp_codes",
                columns: new[] { "UserId", "CodeHash" },
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
                name: "IX_user_library_entries_GameId",
                table: "user_library_entries",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_AddedAt",
                table: "user_library_entries",
                column: "AddedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_CustomAgentConfigJson",
                table: "user_library_entries",
                column: "CustomAgentConfigJson")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId",
                table: "user_library_entries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId_GameId",
                table: "user_library_entries",
                columns: new[] { "UserId", "GameId" },
                unique: true);

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
                name: "admin_report_executions");

            migrationBuilder.DropTable(
                name: "agent_configurations");

            migrationBuilder.DropTable(
                name: "agent_feedback");

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
                name: "api_key_usage_logs");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "cache_stats");

            migrationBuilder.DropTable(
                name: "chat_logs");

            migrationBuilder.DropTable(
                name: "chat_thread_collections");

            migrationBuilder.DropTable(
                name: "chunked_upload_sessions");

            migrationBuilder.DropTable(
                name: "game_errata");

            migrationBuilder.DropTable(
                name: "game_faqs");

            migrationBuilder.DropTable(
                name: "game_state_snapshots");

            migrationBuilder.DropTable(
                name: "game_state_templates");

            migrationBuilder.DropTable(
                name: "library_share_links");

            migrationBuilder.DropTable(
                name: "llm_cost_logs");

            migrationBuilder.DropTable(
                name: "n8n_configs");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "oauth_accounts");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "prompt_audit_logs");

            migrationBuilder.DropTable(
                name: "prompt_evaluation_results");

            migrationBuilder.DropTable(
                name: "quick_questions");

            migrationBuilder.DropTable(
                name: "rule_atoms");

            migrationBuilder.DropTable(
                name: "rulebook_analyses");

            migrationBuilder.DropTable(
                name: "rulespec_comments");

            migrationBuilder.DropTable(
                name: "share_links");

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
                name: "system_configurations");

            migrationBuilder.DropTable(
                name: "temp_sessions");

            migrationBuilder.DropTable(
                name: "text_chunks");

            migrationBuilder.DropTable(
                name: "used_totp_codes");

            migrationBuilder.DropTable(
                name: "user_backup_codes");

            migrationBuilder.DropTable(
                name: "user_library_entries");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "validation_accuracy_baselines");

            migrationBuilder.DropTable(
                name: "vector_documents");

            migrationBuilder.DropTable(
                name: "workflow_error_logs");

            migrationBuilder.DropTable(
                name: "admin_reports");

            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropTable(
                name: "chats");

            migrationBuilder.DropTable(
                name: "game_session_states");

            migrationBuilder.DropTable(
                name: "prompt_versions");

            migrationBuilder.DropTable(
                name: "rule_specs");

            migrationBuilder.DropTable(
                name: "ChatThreads");

            migrationBuilder.DropTable(
                name: "game_categories");

            migrationBuilder.DropTable(
                name: "game_designers");

            migrationBuilder.DropTable(
                name: "game_mechanics");

            migrationBuilder.DropTable(
                name: "game_publishers");

            migrationBuilder.DropTable(
                name: "pdf_documents");

            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropTable(
                name: "prompt_templates");

            migrationBuilder.DropTable(
                name: "document_collections");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "shared_games");
        }
    }
}
