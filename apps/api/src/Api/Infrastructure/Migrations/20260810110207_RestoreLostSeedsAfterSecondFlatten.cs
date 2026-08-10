using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreLostSeedsAfterSecondFlatten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #3633: gli stessi due seed sono andati persi per la SECONDA volta.
            //
            //   2026-06-08  squash #1  (20260608133755_InitialCreate)          → seed persi
            //   2026-07-11  #2785      (RestoreLostThresholdsAndCategorySeeds) → ripristinati
            //   2026-08-03  flatten #2 (#3518, 20260803112713_InitialCreate)   → persi di nuovo
            //
            // La prima perdita fu scoperta subito. La seconda no, perché nel frattempo nessun gate
            // eseguiva più i test di integrazione (#3629: dev-async in timeout; #3632: ci.yml verde
            // senza eseguire nulla). È riemersa solo quando i gate hanno ripreso a riportare, fra i
            // 57 rossi che hanno prodotto.
            //
            // Non riguarda solo i test: su un database creato da zero manca la riga singleton e
            // CertificationThresholdsConfigRepository.GetAsync lancia InvalidOperationException →
            // HTTP 500. Il flatten #2 è già su staging, non ancora in produzione.
            //
            // Raw SQL con ON CONFLICT invece di HasData/InsertData, per la stessa ragione
            // documentata in #2785: i DB esistenti hanno già le 8 categorie con GUID casuali (il
            // vecchio seed gen_random_uuid()), quindi un InsertData a GUID fissi collidereb­be
            // sull'indice UNIQUE(name) e romperebbe il deploy. ON CONFLICT converge senza duplicare.
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
            // DO NOTHING e non DO UPDATE: su un DB esistente le 8 righe ci sono già e possono
            // portare modifiche fatte dagli admin (emoji/color/slug via il CRUD di #1440).
            // Riportarle ai default le sovrascriverebbe.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op, come in #2785: è dato di riferimento a cui altre righe
            // (shared_game_categories, analisi delle meccaniche) possono già puntare. Rimuoverlo
            // rischierebbe cancellazioni a cascata e ri-romperebbe i database nuovi. Il seed è
            // convergente (ON CONFLICT), quindi un rollback non avrebbe nulla da annullare.
        }
    }
}
