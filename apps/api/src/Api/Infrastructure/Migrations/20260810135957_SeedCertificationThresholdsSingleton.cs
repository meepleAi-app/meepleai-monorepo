using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #3646 — ripristina i due seed persi in uno squash di migration.
    ///
    /// <para>
    /// <b>Causa, ricostruita dalla history.</b> Non è stato l'appiattimento #3518 (2026-08-03), come
    /// si poteva supporre: già la migration che quello schiacciava conteneva un solo seed. I seed
    /// sono spariti nello squash precedente — <c>eff570a08</c>, «squash 81 migrations into
    /// InitialCreate» (#2021) — che ha eliminato
    /// <c>20260424094641_M2_0_MechanicGoldenAndValidation</c> (soglie) e
    /// <c>20260522145355_AddVisualMetadataToGameCategory</c> (categorie) senza riportarne gli INSERT.
    /// </para>
    /// <para>
    /// <b>Perché non se n'era accorto nessuno.</b> Staging e produzione hanno database preesistenti
    /// allo squash, quindi conservano i dati inseriti dalle migration originali; a nascere vuoto è
    /// solo un database creato da zero — i database di test, e qualunque nuovo ambiente o
    /// disaster-recovery. Su staging infatti <c>game_categories</c> ha righe, mentre
    /// <c>certification_thresholds_config</c> è vuota (quella tabella è più recente dello squash).
    /// </para>
    /// <para>
    /// I valori sono ripresi alla lettera dalle migration originali recuperate dalla history, non
    /// reinventati: <c>(1, 70, 10, 80, 60)</c> coincide con <c>CertificationThresholds.Default()</c>.
    /// </para>
    /// <para>
    /// Il seed sta nello schema e non in un seeder applicativo perché è lo schema stesso a dichiarare
    /// il singleton (<c>ck_certification_thresholds_config_singleton</c>): una tabella che ammette
    /// esattamente una riga e non ce l'ha è in uno stato che il DB considera valido ma il dominio no.
    /// </para>
    /// </summary>
    public partial class SeedCertificationThresholdsSingleton : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente per due motivi: la migration può incontrare un DB dove un operatore ha già
            // inserito la riga a mano per sbloccarsi, e non deve MAI sovrascrivere soglie che un
            // admin ha personalizzato via /admin/mechanic-extractor/thresholds.
            migrationBuilder.Sql(
                """
                INSERT INTO certification_thresholds_config
                    (id, min_coverage_pct, max_page_tolerance, min_bgg_match_pct, min_overall_score, updated_at, updated_by_user_id)
                VALUES
                    (1, 70, 10, 80, 60, NOW() AT TIME ZONE 'UTC', NULL)
                ON CONFLICT (id) DO NOTHING;
                """);

            // Le 8 categorie di default, perse nello stesso squash. `DO NOTHING` e non `DO UPDATE`
            // come nell'originale: là serviva ad aggiungere emoji/colore a righe già esistenti, qui
            // sovrascriverebbe personalizzazioni fatte da allora. Su un DB che le ha già (staging,
            // produzione) questo statement non tocca nulla.
            //
            // #3650: `ON CONFLICT` SENZA target di inferenza, non `ON CONFLICT (name)`.
            // `game_categories` ha DUE vincoli unique — `ix_game_categories_name` e
            // `ix_game_categories_slug` — e `UpdateGameCategoryCommand` (#1440) espone `Name` e
            // `Slug` come campi indipendenti. Un admin che rinomina «Strategy → Strategia» lasciando
            // `slug = 'strategy'` crea una riga su cui l'INSERT non trova conflitto sul nome,
            // procede, e viola l'indice sullo slug: `23505 duplicate key value violates unique
            // constraint "ix_game_categories_slug"` → migration abortita, deploy bloccato.
            // Senza target, QUALUNQUE violazione di unicità viene assorbita — che è l'intento del
            // seed: se la riga esiste in qualsiasi forma, non fare nulla.
            migrationBuilder.Sql(
                """
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
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Il rollback rimuove SOLO la riga di default lasciata intatta. Se l'admin ha cambiato
            // anche un solo valore, la riga è sua e va preservata: cancellarla trasformerebbe un
            // rollback di schema in una perdita di configurazione.
            migrationBuilder.Sql(
                """
                DELETE FROM certification_thresholds_config
                WHERE id = 1
                  AND min_coverage_pct = 70
                  AND max_page_tolerance = 10
                  AND min_bgg_match_pct = 80
                  AND min_overall_score = 60
                  AND updated_by_user_id IS NULL;
                """);

            // Le categorie NON vengono rimosse: sono referenziate dai giochi del catalogo, e un
            // rollback che le cancellasse romperebbe le FK o svuoterebbe la categorizzazione. Un
            // seed idempotente di righe condivise non ha un inverso sicuro — meglio lasciarle.
        }
    }
}
