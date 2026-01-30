using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTypologyAndPromptTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            // Seed default agent typologies (ISSUE-3175)
            // Note: Using admin@example.com user as creator/approver
            var rulesExpertId = Guid.NewGuid();
            var quickStartId = Guid.NewGuid();
            var ledgerMasterId = Guid.NewGuid();

            // Get admin user ID (first user with email admin@example.com or first user in system)
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    v_admin_user_id uuid;
                    v_rules_expert_id uuid := '" + rulesExpertId + @"'::uuid;
                    v_quick_start_id uuid := '" + quickStartId + @"'::uuid;
                    v_ledger_master_id uuid := '" + ledgerMasterId + @"'::uuid;
                BEGIN
                    -- Get admin user (first user or admin@example.com)
                    SELECT ""Id"" INTO v_admin_user_id FROM users
                    WHERE ""Email"" = 'admin@example.com' OR ""Role"" = 'admin'
                    LIMIT 1;

                    -- Fallback to first user if no admin found
                    IF v_admin_user_id IS NULL THEN
                        SELECT ""Id"" INTO v_admin_user_id FROM users ORDER BY ""CreatedAt"" LIMIT 1;
                    END IF;

                    -- Skip seeding if no users exist yet
                    IF v_admin_user_id IS NOT NULL THEN
                        -- Seed Rules Expert typology
                        INSERT INTO agent_typologies (id, name, description, base_prompt, default_strategy_json, status, created_by, approved_by, created_at, approved_at, is_deleted)
                        VALUES (
                            v_rules_expert_id,
                            'Rules Expert',
                            'Explains game rules and resolves doubts with precise rule citations',
                            'You are a Rules Expert for {{gameTitle}}. Your role is to provide accurate, rule-based answers to player questions. Always cite specific rule sections when possible.',
                            '{""Name"":""HybridSearch"",""Parameters"":{""VectorWeight"":0.8,""KeywordWeight"":0.2,""TopK"":5,""MinScore"":0.55}}',
                            2, -- Approved
                            v_admin_user_id,
                            v_admin_user_id,
                            NOW(),
                            NOW(),
                            false
                        );

                        -- Seed Quick Start typology
                        INSERT INTO agent_typologies (id, name, description, base_prompt, default_strategy_json, status, created_by, approved_by, created_at, approved_at, is_deleted)
                        VALUES (
                            v_quick_start_id,
                            'Quick Start',
                            'Provides fast setup guidance and first-game explanations',
                            'You are a Quick Start Guide for {{gameTitle}}. Help new players get started quickly with setup instructions and first-turn guidance. Focus on essential rules for the first game.',
                            '{""Name"":""HybridSearch"",""Parameters"":{""VectorWeight"":0.7,""KeywordWeight"":0.3,""TopK"":10,""MinScore"":0.55}}',
                            2, -- Approved
                            v_admin_user_id,
                            v_admin_user_id,
                            NOW(),
                            NOW(),
                            false
                        );

                        -- Seed Ledger Master typology
                        INSERT INTO agent_typologies (id, name, description, base_prompt, default_strategy_json, status, created_by, approved_by, created_at, approved_at, is_deleted)
                        VALUES (
                            v_ledger_master_id,
                            'Ledger Master',
                            'Tracks complete game state and suggests optimal moves',
                            'You are a Ledger Master for {{gameTitle}}. Track the complete game state from conversation and provide strategic analysis and move suggestions based on current position.',
                            '{""Name"":""VectorOnly"",""Parameters"":{""TopK"":10,""MinScore"":0.80}}',
                            2, -- Approved
                            v_admin_user_id,
                            v_admin_user_id,
                            NOW(),
                            NOW(),
                            false
                        );

                        -- Seed initial prompt templates (version 1, current)
                        INSERT INTO typology_prompt_templates (id, typology_id, content, version, is_current, created_by, created_at)
                        VALUES
                            (gen_random_uuid(), v_rules_expert_id, 'You are a Rules Expert for {{gameTitle}}. Your role is to provide accurate, rule-based answers to player questions. Always cite specific rule sections when possible.', 1, true, v_admin_user_id, NOW()),
                            (gen_random_uuid(), v_quick_start_id, 'You are a Quick Start Guide for {{gameTitle}}. Help new players get started quickly with setup instructions and first-turn guidance. Focus on essential rules for the first game.', 1, true, v_admin_user_id, NOW()),
                            (gen_random_uuid(), v_ledger_master_id, 'You are a Ledger Master for {{gameTitle}}. Track the complete game state from conversation and provide strategic analysis and move suggestions based on current position.', 1, true, v_admin_user_id, NOW());
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "typology_prompt_templates");

            migrationBuilder.DropTable(
                name: "agent_typologies");
        }
    }
}
