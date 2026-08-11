using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SharedGameProviderCredentialXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Safety Gate (#1087, rollback-runbook §8.2): i DROP COLUMN qui sotto sono
            // rollback-safe perché le colonne sono già di fatto morte. Nessun trigger nello schema
            // le popola — `ef_update_row_version()` è stato rimosso da #2305 e non esiste in alcuna
            // migration — e Postgres non valorizza una `bytea` da sé, quindi contengono NULL su
            // ogni riga. Nessun codice le legge: non sono esposte da alcun DTO né dal frontend.
            // Il Down() le ricrea identiche.
            migrationBuilder.Sql("-- safe: drop dead bytea concurrency columns, NULL on every row (no trigger populates them since #2305); replaced by the xmin system column (§8.3)");

            // #3651: rimuove le colonne `row_version` (bytea), residuo del meccanismo pre-#2305.
            // Finché esistevano, EF confrontava NULL = NULL a ogni update e nessun conflitto veniva
            // rilevato: la concorrenza ottimistica era dichiarata dalla configurazione ma inattiva.
            // Misurato prima della conversione — due scritture concorrenti riuscivano entrambe:
            //   SharedGameXminConcurrencyTests.ConcurrentEdits_SecondWriterThrowsConcurrencyException
            //   ProviderCredentialXminConcurrencyTests.ConcurrentDeactivations_SecondWriterThrowsConcurrencyException
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "provider_credentials");

            // NB: nessun AddColumn per `xmin`, benché lo scaffold di EF ne avesse generati due.
            // `xmin` è una colonna di SISTEMA di PostgreSQL, presente su ogni tabella: un
            // `ADD COLUMN xmin` fallirebbe con «column name "xmin" conflicts with a system column
            // name». EF la vede come una proprietà nuova del modello e non sa che è di sistema —
            // stessa ragione per cui il comando ha avvertito di una possibile perdita di dati.
            // Identico a quanto fatto per pdf_documents in 20260811130532 (#3658).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina le colonne, non un meccanismo che le valorizzi: tornando indietro il token
            // resterebbe NULL come prima di questa migration. È esattamente lo stato precedente.
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "shared_games",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "provider_credentials",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}
