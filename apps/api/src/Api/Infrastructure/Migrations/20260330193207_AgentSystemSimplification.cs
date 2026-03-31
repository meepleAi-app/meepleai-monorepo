using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgentSystemSimplification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add new columns to agent_definitions FIRST (needed before data migration SQL)
            migrationBuilder.AddColumn<Guid>(
                name: "game_id",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "invocation_count",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_system_defined",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_invoked_at",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "typology_slug",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Step 2: Migrate agent_typologies (public schema) → agent_definitions (conditional — may already be empty)
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'agent_typologies'
                    ) THEN
                        INSERT INTO knowledge_base.agent_definitions
                            (id, name, description, type_value, type_description,
                             model, temperature, max_tokens,
                             is_system_defined, typology_slug,
                             status, is_active, created_at,
                             kb_card_ids, prompts, strategy, tools, chat_language)
                        SELECT
                            id,
                            name,
                            COALESCE(description, ''),
                            'system',
                            name,
                            'meta-llama/llama-3.3-70b-instruct:free',
                            0.3,
                            2048,
                            TRUE,
                            LOWER(REPLACE(name, ' ', '-')),
                            2,
                            TRUE,
                            created_at,
                            '[]',
                            '[]',
                            '{""Name"":""HybridSearch"",""Parameters"":{}}',
                            '[]',
                            'auto'
                        FROM agent_typologies
                        WHERE is_deleted = FALSE
                        ON CONFLICT DO NOTHING;
                    END IF;
                END $$;
            ");

            // Step 3: Drop foreign keys referencing the tables we are about to drop
            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_agent_typologies_TypologyId",
                table: "agent_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_agents_AgentId",
                table: "agent_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_agent_test_results_agent_typologies_typology_id",
                table: "agent_test_results");

            migrationBuilder.DropForeignKey(
                name: "FK_chats_agents_AgentId",
                table: "chats");

            // Step 4: Drop legacy tables (now safe — data migrated, FKs removed)
            migrationBuilder.DropTable(
                name: "agent_configurations");

            migrationBuilder.DropTable(
                name: "agent_game_state_snapshots");

            migrationBuilder.DropTable(
                name: "typology_prompt_templates");

            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropTable(
                name: "agent_typologies");

            // Step 5: Drop stale indexes
            migrationBuilder.DropIndex(
                name: "IX_chats_AgentId",
                table: "chats");

            migrationBuilder.DropIndex(
                name: "IX_agent_sessions_AgentId",
                table: "agent_sessions");

            migrationBuilder.DropIndex(
                name: "IX_agent_sessions_TypologyId",
                table: "agent_sessions");

            // Step 6: Remove the now-redundant AgentId column from agent_sessions
            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "agent_sessions");

            // Step 7: Rename TypologyId → agent_definition_id (data stays intact)
            migrationBuilder.RenameColumn(
                name: "TypologyId",
                table: "agent_sessions",
                newName: "agent_definition_id");

            // Step 8: Add FK from agent_sessions.agent_definition_id → agent_definitions.id
            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_agent_definition_id",
                table: "agent_sessions",
                column: "agent_definition_id");

            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_agent_definitions_agent_definition_id",
                table: "agent_sessions",
                column: "agent_definition_id",
                principalSchema: "knowledge_base",
                principalTable: "agent_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Step 9: Index for typology_slug lookups
            migrationBuilder.CreateIndex(
                name: "ix_agent_definitions_typology_slug",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "typology_slug");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse Step 8-9
            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_agent_definitions_agent_definition_id",
                table: "agent_sessions");

            migrationBuilder.DropIndex(
                name: "IX_agent_sessions_agent_definition_id",
                table: "agent_sessions");

            migrationBuilder.DropIndex(
                name: "ix_agent_definitions_typology_slug",
                schema: "knowledge_base",
                table: "agent_definitions");

            // Reverse Step 1 (new agent_definitions columns)
            migrationBuilder.DropColumn(
                name: "game_id",
                schema: "knowledge_base",
                table: "agent_definitions");

            migrationBuilder.DropColumn(
                name: "invocation_count",
                schema: "knowledge_base",
                table: "agent_definitions");

            migrationBuilder.DropColumn(
                name: "is_system_defined",
                schema: "knowledge_base",
                table: "agent_definitions");

            migrationBuilder.DropColumn(
                name: "last_invoked_at",
                schema: "knowledge_base",
                table: "agent_definitions");

            migrationBuilder.DropColumn(
                name: "typology_slug",
                schema: "knowledge_base",
                table: "agent_definitions");

            // Reverse Step 7
            migrationBuilder.RenameColumn(
                name: "agent_definition_id",
                table: "agent_sessions",
                newName: "TypologyId");

            // Reverse Step 6
            migrationBuilder.AddColumn<Guid>(
                name: "AgentId",
                table: "agent_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Reverse Step 4: Recreate legacy tables
            migrationBuilder.CreateTable(
                name: "agent_game_state_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    board_state_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turn_number = table.Column<int>(type: "integer", nullable: false)
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
                name: "agent_typologies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    base_prompt = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    default_strategy_json = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_typologies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvocationCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastInvokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    StrategyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StrategyParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                name: "typology_prompt_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    typology_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    version = table.Column<int>(type: "integer", nullable: false)
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
                name: "agent_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_mode = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    llm_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    llm_provider = table.Column<int>(type: "integer", nullable: false),
                    max_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 4096),
                    selected_document_ids_json = table.Column<string>(type: "jsonb", nullable: true),
                    system_prompt_override = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    temperature = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 0.7m)
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

            // Reverse Step 5: Recreate old indexes
            migrationBuilder.CreateIndex(
                name: "IX_chats_AgentId",
                table: "chats",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_AgentId",
                table: "agent_sessions",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_TypologyId",
                table: "agent_sessions",
                column: "TypologyId");

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

            // Reverse Step 3: Restore old FKs
            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_agent_typologies_TypologyId",
                table: "agent_sessions",
                column: "TypologyId",
                principalTable: "agent_typologies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_agents_AgentId",
                table: "agent_sessions",
                column: "AgentId",
                principalTable: "agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_test_results_agent_typologies_typology_id",
                table: "agent_test_results",
                column: "typology_id",
                principalTable: "agent_typologies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chats_agents_AgentId",
                table: "chats",
                column: "AgentId",
                principalTable: "agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
