using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogSeedDraftAbTestSessionXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Safety Gate (#1087, rollback-runbook §8.2): i DROP COLUMN qui sotto sono
            // rollback-safe. Su `catalog_seed_drafts` la colonna è morta come nei lotti precedenti:
            // nessun trigger la popola (`ef_update_row_version()` rimosso da #2305), Postgres non
            // valorizza una `bytea`, quindi contiene NULL su ogni riga.
            //
            // Su `knowledge_base.ab_test_sessions` la situazione è PEGGIORE e va detta: là la
            // colonna era `bytea NOT NULL`, quindi non conteneva NULL — non conteneva nulla,
            // perché ogni INSERT falliva con «23502: null value in column "row_version" … violates
            // not-null constraint». La tabella è, di fatto, vuota e non scrivibile. Il DROP non può
            // perdere dati che non esistono.
            migrationBuilder.Sql("-- safe: drop dead bytea concurrency columns; NULL on every row (catalog_seed_drafts) or table not writable at all (ab_test_sessions, 23502 on INSERT); replaced by the xmin system column (§8.3)");

            // #3651 lotto 5.
            //
            // `catalog_seed_drafts`: il guasto consueto del lotto — EF confrontava NULL = NULL a
            // ogni update, nessun conflitto rilevato. Misurato prima della conversione:
            //   CatalogSeedDraftXminConcurrencyTests.Update_AfterConcurrentWrite_… → «no exception was thrown»
            //
            // `ab_test_sessions`: NON era una protezione inefficace, era una tabella inutilizzabile.
            // Il token era `byte[]` non-nullable, quindi la colonna generata è `bytea NOT NULL` e
            // nessuno la popolava: creare una sessione A/B falliva sempre in produzione. Invisibile
            // perché NESSUN test di AbTestSession tocca Postgres — gli handler girano su InMemory,
            // dove il vincolo non esiste. Trovato dal test di concorrenza scritto per questo lotto.
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "catalog_seed_drafts");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "knowledge_base",
                table: "ab_test_sessions");

            // NB: nessun AddColumn per `xmin`, benché lo scaffold di EF ne avesse generati due.
            // `xmin` è una colonna di SISTEMA di PostgreSQL, presente su ogni tabella: un
            // `ADD COLUMN xmin` fallirebbe con «column name "xmin" conflicts with a system column
            // name». EF la vede come una proprietà nuova del modello e non sa che è di sistema.
            // Quinta volta che va corretto a mano: 20260811130532 (#3658), 20260811224354 (#3683),
            // 20260812035720 (#3698), 20260816120212 (#3710).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina le colonne, non un meccanismo che le valorizzi: tornando indietro il token
            // resterebbe NULL come prima di questa migration. Nessun DropColumn per `xmin`, per la
            // ragione detta nell'Up(): non è stata aggiunta.
            //
            // ⚠️ Il Down() su `ab_test_sessions` ripristina anche il vincolo NOT NULL su una colonna
            // che nulla popola: torna cioè a una tabella non scrivibile. È lo stato precedente, ed
            // è corretto che un rollback lo ricrei — ma chi lo esegue deve sapere che sta
            // reintroducendo un difetto, non solo una colonna.
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "catalog_seed_drafts",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                schema: "knowledge_base",
                table: "ab_test_sessions",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
