using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M1_5_MechanicCardsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent raw SQL (ADR-051 AC-1, pattern PR #517). Each statement is terminated with
            // ';' so the deploy's idempotent psql path parses cleanly (#2766).
            //
            // NOTE: the `xmin` concurrency token maps to PostgreSQL's SYSTEM column of the same name,
            // which every table already has — it is deliberately NOT declared here (declaring a user
            // column named `xmin` would collide with the system column).

            migrationBuilder.Sql(
                "ALTER TABLE mechanic_analyses ADD COLUMN IF NOT EXISTS published_card_id uuid NULL;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS mechanic_cards (
                    id uuid NOT NULL,
                    shared_game_id uuid NOT NULL,
                    origin_analysis_id uuid NOT NULL,
                    origin character varying(32) NOT NULL,
                    title character varying(200) NOT NULL,
                    content jsonb NOT NULL,
                    version integer NOT NULL DEFAULT 1,
                    is_suppressed boolean NOT NULL DEFAULT false,
                    suppressed_reason character varying(500) NULL,
                    suppressed_at timestamp with time zone NULL,
                    suppressed_by uuid NULL,
                    error_reports_count integer NOT NULL DEFAULT 0,
                    feedback_score numeric(4,2) NULL,
                    published_at timestamp with time zone NOT NULL,
                    published_by uuid NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_mechanic_cards"" PRIMARY KEY (id),
                    CONSTRAINT ck_mechanic_cards_error_reports_non_negative CHECK (error_reports_count >= 0),
                    CONSTRAINT ck_mechanic_cards_origin CHECK (origin IN ('ai_reviewed', 'manual', 'imported_external')),
                    CONSTRAINT ck_mechanic_cards_suppression_completeness CHECK ((is_suppressed = false AND suppressed_at IS NULL AND suppressed_by IS NULL AND suppressed_reason IS NULL) OR (is_suppressed = true AND suppressed_at IS NOT NULL AND suppressed_by IS NOT NULL AND suppressed_reason IS NOT NULL)),
                    CONSTRAINT ck_mechanic_cards_version_positive CHECK (version >= 1),
                    CONSTRAINT ""FK_mechanic_cards_mechanic_analyses_origin_analysis_id"" FOREIGN KEY (origin_analysis_id) REFERENCES mechanic_analyses (id) ON DELETE RESTRICT,
                    CONSTRAINT ""FK_mechanic_cards_shared_games_shared_game_id"" FOREIGN KEY (shared_game_id) REFERENCES shared_games (id) ON DELETE CASCADE
                );");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS mechanic_card_audit_log (
                    id uuid NOT NULL,
                    card_id uuid NOT NULL,
                    action character varying(32) NOT NULL,
                    actor_id uuid NOT NULL,
                    occurred_at timestamp with time zone NOT NULL,
                    metadata jsonb NULL,
                    CONSTRAINT ""PK_mechanic_card_audit_log"" PRIMARY KEY (id),
                    CONSTRAINT ck_mechanic_card_audit_log_action CHECK (action IN ('published', 'suppressed', 'unsuppressed', 'revised')),
                    CONSTRAINT ""FK_mechanic_card_audit_log_mechanic_cards_card_id"" FOREIGN KEY (card_id) REFERENCES mechanic_cards (id) ON DELETE CASCADE
                );");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_mechanic_card_audit_log_card_id ON mechanic_card_audit_log (card_id);");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_mechanic_cards_origin_analysis_id ON mechanic_cards (origin_analysis_id);");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_mechanic_cards_active_per_game ON mechanic_cards (shared_game_id) WHERE is_suppressed = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS mechanic_card_audit_log;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS mechanic_cards;");
            migrationBuilder.Sql("ALTER TABLE mechanic_analyses DROP COLUMN IF EXISTS published_card_id;");
        }
    }
}
