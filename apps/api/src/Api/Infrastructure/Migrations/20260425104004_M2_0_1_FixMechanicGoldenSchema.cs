using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Forward-only fix for Sprint 1 (M2_0) schema drift vs. the EF entity model.
    ///
    /// Sprint 1 created mechanic_golden_claims via raw SQL with two columns that don't
    /// match <see cref="Api.Infrastructure.Entities.SharedGameCatalog.MechanicGoldenClaimEntity"/>:
    ///   - keywords text[] (entity expects keywords_json jsonb)
    ///   - missing is_deleted boolean (entity has IsDeleted soft-delete + global query filter)
    ///
    /// Sprint 1 also created mechanic_analysis_metrics with column names that don't match
    /// <see cref="Api.Infrastructure.Entities.SharedGameCatalog.MechanicAnalysisMetricsEntity"/>:
    ///   - thresholds_snapshot (entity expects thresholds_snapshot_json)
    ///   - match_details (entity expects match_details_json)
    ///
    /// Without this fix, every EF query against MechanicGoldenClaim fails with
    /// PostgresException 42703 ("column m.is_deleted does not exist"), blocking Task 3
    /// (PuertoRicoGoldenSeeder) and any downstream Sprint 2 work.
    ///
    /// Down() is intentionally a no-op: reverting Sprint 1's schema is out of scope.
    /// Staging has not yet applied Sprint 1, so this migration runs cleanly on a fresh DB
    /// already migrated to M2_0.
    /// </remarks>
    public partial class M2_0_1_FixMechanicGoldenSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        -- Fix 1: mechanic_golden_claims — soft-delete column + jsonb keywords
        ALTER TABLE mechanic_golden_claims
            ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT false;

        ALTER TABLE mechanic_golden_claims
            ADD COLUMN IF NOT EXISTS keywords_json jsonb NOT NULL DEFAULT '[]'::jsonb;

        -- Preserve any existing keywords (text[]) data into keywords_json (jsonb) before drop.
        -- to_jsonb(text[]) produces a JSON array of strings, matching the entity contract.
        UPDATE mechanic_golden_claims
            SET keywords_json = to_jsonb(keywords)
            WHERE keywords IS NOT NULL
              AND array_length(keywords, 1) > 0
              AND keywords_json = '[]'::jsonb;

        ALTER TABLE mechanic_golden_claims DROP COLUMN IF EXISTS keywords;

        -- Replace the partial soft-delete index (now redundant — HasQueryFilter handles it).
        DROP INDEX IF EXISTS ix_mechanic_golden_claims_shared_game_id;
        CREATE INDEX IF NOT EXISTS ix_mechanic_golden_claims_shared_game_id
            ON mechanic_golden_claims(shared_game_id);

        -- Composite index expected by entity config (shared_game_id, section).
        CREATE INDEX IF NOT EXISTS ix_mechanic_golden_claims_shared_game_section
            ON mechanic_golden_claims(shared_game_id, section);

        -- Section range check expected by entity config.
        ALTER TABLE mechanic_golden_claims
            DROP CONSTRAINT IF EXISTS ck_mechanic_golden_claims_section_range;
        ALTER TABLE mechanic_golden_claims
            ADD CONSTRAINT ck_mechanic_golden_claims_section_range
            CHECK (section BETWEEN 0 AND 5);

        -- Expected-page check uses entity's '> 0' wording (compatible with Sprint 1's '>= 1').
        ALTER TABLE mechanic_golden_claims
            DROP CONSTRAINT IF EXISTS mechanic_golden_claims_expected_page_check;
        ALTER TABLE mechanic_golden_claims
            DROP CONSTRAINT IF EXISTS ck_mechanic_golden_claims_expected_page_positive;
        ALTER TABLE mechanic_golden_claims
            ADD CONSTRAINT ck_mechanic_golden_claims_expected_page_positive
            CHECK (expected_page > 0);

        -- Fix 2: mechanic_analysis_metrics — column names must match entity (_json suffix).
        DO $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'mechanic_analysis_metrics'
                  AND column_name = 'thresholds_snapshot'
            ) THEN
                ALTER TABLE mechanic_analysis_metrics
                    RENAME COLUMN thresholds_snapshot TO thresholds_snapshot_json;
            END IF;

            IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'mechanic_analysis_metrics'
                  AND column_name = 'match_details'
            ) THEN
                ALTER TABLE mechanic_analysis_metrics
                    RENAME COLUMN match_details TO match_details_json;
            END IF;
        END$$;
    ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only fix: reverting Sprint 1's broken schema is out of scope.
            // To roll back further than M2_0, drop the database or use Sprint 1's Down().
        }
    }
}
