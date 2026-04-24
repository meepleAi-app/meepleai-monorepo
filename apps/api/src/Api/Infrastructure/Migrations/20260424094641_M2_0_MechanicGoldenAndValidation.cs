using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M2_0_MechanicGoldenAndValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        ALTER TABLE mechanic_analyses
        ADD COLUMN certification_status integer NOT NULL DEFAULT 0,
        ADD COLUMN certified_at timestamptz NULL,
        ADD COLUMN certified_by_user_id uuid NULL REFERENCES ""Users""(""Id"") ON DELETE SET NULL,
        ADD COLUMN certification_override_reason text NULL,
        ADD COLUMN last_metrics_id uuid NULL;

        CREATE TABLE mechanic_golden_claims (
            id uuid PRIMARY KEY,
            shared_game_id uuid NOT NULL REFERENCES shared_games(id) ON DELETE CASCADE,
            section integer NOT NULL,
            statement text NOT NULL CHECK (length(statement) BETWEEN 1 AND 500),
            expected_page integer NOT NULL CHECK (expected_page >= 1),
            source_quote text NOT NULL CHECK (length(source_quote) BETWEEN 1 AND 1000),
            keywords text[] NOT NULL DEFAULT '{}',
            embedding vector(768) NULL,
            curator_user_id uuid NOT NULL REFERENCES ""Users""(""Id"") ON DELETE RESTRICT,
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now(),
            deleted_at timestamptz NULL,
            xmin xid NOT NULL DEFAULT 0
        );
        CREATE INDEX ix_mechanic_golden_claims_shared_game_id ON mechanic_golden_claims(shared_game_id) WHERE deleted_at IS NULL;
        CREATE INDEX ix_mechanic_golden_claims_embedding ON mechanic_golden_claims USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);

        CREATE TABLE mechanic_golden_bgg_tags (
            id uuid PRIMARY KEY,
            shared_game_id uuid NOT NULL REFERENCES shared_games(id) ON DELETE CASCADE,
            name text NOT NULL CHECK (length(name) BETWEEN 1 AND 200),
            category text NOT NULL CHECK (length(category) BETWEEN 1 AND 100),
            imported_at timestamptz NOT NULL DEFAULT now(),
            UNIQUE(shared_game_id, name)
        );
        CREATE INDEX ix_mechanic_golden_bgg_tags_shared_game_id ON mechanic_golden_bgg_tags(shared_game_id);

        CREATE TABLE mechanic_analysis_metrics (
            id uuid PRIMARY KEY,
            mechanic_analysis_id uuid NOT NULL REFERENCES mechanic_analyses(id) ON DELETE CASCADE,
            shared_game_id uuid NOT NULL REFERENCES shared_games(id) ON DELETE CASCADE,
            coverage_pct numeric(5,2) NOT NULL CHECK (coverage_pct BETWEEN 0 AND 100),
            page_accuracy_pct numeric(5,2) NOT NULL CHECK (page_accuracy_pct BETWEEN 0 AND 100),
            bgg_match_pct numeric(5,2) NOT NULL CHECK (bgg_match_pct BETWEEN 0 AND 100),
            overall_score numeric(5,2) NOT NULL CHECK (overall_score BETWEEN 0 AND 100),
            certification_status integer NOT NULL,
            golden_version_hash char(64) NOT NULL,
            thresholds_snapshot jsonb NOT NULL,
            match_details jsonb NOT NULL,
            computed_at timestamptz NOT NULL DEFAULT now()
        );
        CREATE INDEX ix_mechanic_analysis_metrics_analysis_computed ON mechanic_analysis_metrics(mechanic_analysis_id, computed_at DESC);
        CREATE INDEX ix_mechanic_analysis_metrics_shared_game_id ON mechanic_analysis_metrics(shared_game_id);

        CREATE TABLE certification_thresholds_config (
            id integer PRIMARY KEY CHECK (id = 1),
            min_coverage_pct numeric(5,2) NOT NULL,
            max_page_tolerance integer NOT NULL,
            min_bgg_match_pct numeric(5,2) NOT NULL,
            min_overall_score numeric(5,2) NOT NULL,
            updated_at timestamptz NOT NULL DEFAULT now(),
            updated_by_user_id uuid NULL REFERENCES ""Users""(""Id"") ON DELETE SET NULL,
            xmin xid NOT NULL DEFAULT 0
        );
        INSERT INTO certification_thresholds_config (id, min_coverage_pct, max_page_tolerance, min_bgg_match_pct, min_overall_score)
        VALUES (1, 70, 10, 80, 60) ON CONFLICT DO NOTHING;

        ALTER TABLE mechanic_analyses
        ADD CONSTRAINT fk_mechanic_analyses_last_metrics
        FOREIGN KEY (last_metrics_id) REFERENCES mechanic_analysis_metrics(id)
        DEFERRABLE INITIALLY DEFERRED ON DELETE SET NULL;
    ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        ALTER TABLE mechanic_analyses DROP CONSTRAINT IF EXISTS fk_mechanic_analyses_last_metrics;
        DROP TABLE IF EXISTS certification_thresholds_config;
        DROP TABLE IF EXISTS mechanic_analysis_metrics;
        DROP TABLE IF EXISTS mechanic_golden_bgg_tags;
        DROP TABLE IF EXISTS mechanic_golden_claims;
        ALTER TABLE mechanic_analyses
            DROP COLUMN last_metrics_id,
            DROP COLUMN certification_override_reason,
            DROP COLUMN certified_by_user_id,
            DROP COLUMN certified_at,
            DROP COLUMN certification_status;
    ");
        }
    }
}
