using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreLostThresholdsAndCategorySeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #2785: the 2026-06-08 migration squash (20260608133755_InitialCreate) dropped two
            // raw-SQL data seeds that the pre-squash migrations performed:
            //   • M2_0_MechanicGoldenAndValidation → certification_thresholds_config singleton (id=1)
            //   • AddVisualMetadataToGameCategory  → 8 default game_categories
            // Fresh databases (including per-test isolated integration DBs) therefore have no
            // thresholds singleton — GetCertificationThresholds throws InvalidOperationException →
            // HTTP 500 — and no categories. Re-seed idempotently so both fresh AND already-seeded
            // existing databases converge to the same baseline.
            //
            // NOTE: intentionally raw SQL with ON CONFLICT, not HasData/InsertData. Existing
            // prod/staging DBs already hold the 8 categories with RANDOM GUIDs (the old
            // gen_random_uuid() seed), so a fixed-GUID InsertData would collide on the UNIQUE(name)
            // index and break the deploy. ON CONFLICT converges without duplicating.
            // updated_at is NOT NULL with no DB default (the squash dropped M2_0's `DEFAULT now()`),
            // so it must be supplied explicitly here.
            migrationBuilder.Sql(@"
INSERT INTO certification_thresholds_config (id, min_coverage_pct, max_page_tolerance, min_bgg_match_pct, min_overall_score, updated_at)
VALUES (1, 70, 10, 80, 60, NOW())
ON CONFLICT (id) DO NOTHING;");

            migrationBuilder.Sql(@"
INSERT INTO game_categories (id, name, slug, emoji, color, created_at)
VALUES
    (gen_random_uuid(), 'Strategy',      'strategy',      '🎯', '#ef4444', NOW()),
    (gen_random_uuid(), 'Party',         'party',         '🎉', '#ec4899', NOW()),
    (gen_random_uuid(), 'Cooperative',   'cooperative',   '🤝', '#10b981', NOW()),
    (gen_random_uuid(), 'Deck Building', 'deck-building', '🃏', '#8b5cf6', NOW()),
    (gen_random_uuid(), 'Family',        'family',        '👨‍👩‍👧‍👦', '#f59e0b', NOW()),
    (gen_random_uuid(), 'Abstract',      'abstract',      '🔷', '#06b6d4', NOW()),
    (gen_random_uuid(), 'Thematic',      'thematic',      '🗺️', '#ef4444', NOW()),
    (gen_random_uuid(), 'Euro',          'euro',          '🏛️', '#6366f1', NOW())
ON CONFLICT (name) DO NOTHING;");
            // DO NOTHING (not DO UPDATE): this migration only RESTORES categories missing on
            // fresh DBs. On existing DBs the 8 rows are already present and may carry admin edits
            // (emoji/color/slug via the #1440 CRUD) — leave them untouched rather than reverting
            // them to these defaults.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: this migration restores baseline reference data that other rows
            // (shared_game_categories, mechanic analyses) may already reference. Reverting it
            // would risk cascading deletes of live data and re-break fresh databases. The seed is
            // convergent (ON CONFLICT), so a rollback has nothing meaningful to undo.
        }
    }
}
