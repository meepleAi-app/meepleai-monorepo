using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BggTosHashXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Safety Gate (#1087, rollback-runbook §8.2): il DROP COLUMN è rollback-safe
            // perché la colonna è già di fatto morta. Nessun trigger nello schema la popola
            // — `ef_update_row_version()` è stato rimosso da #2305 — e Postgres non valorizza una
            // `bytea` da sé, quindi contiene NULL su ogni riga. Nessun codice la legge: non è
            // esposta da alcun DTO né dal frontend. Il Down() la ricrea identica.
            migrationBuilder.Sql("-- safe: drop dead bytea concurrency column, NULL on every row (no trigger populates it since #2305); replaced by the xmin system column (§8.3)");

            // #3651 lotto 4 — quarta entità allineata al pattern xmin di ADR-060.
            // Finché questa colonna esisteva, EF confrontava NULL = NULL a ogni update e nessun
            // conflitto veniva rilevato. Misurato prima della conversione, il test di concorrenza
            // falliva con «no exception was thrown»:
            //   BggTosHashXminConcurrencyTests.Update_AfterConcurrentWrite_ThrowsConcurrencyException
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "bgg_tos_hashes");

            // NB: nessun AddColumn per `xmin`, benché lo scaffold di EF ne avesse generato uno.
            // `xmin` è una colonna di SISTEMA di PostgreSQL, presente su ogni tabella: un
            // `ADD COLUMN xmin` fallirebbe con «column name "xmin" conflicts with a system column
            // name». EF la vede come una proprietà nuova del modello e non sa che è di sistema —
            // stessa ragione per cui il comando ha avvertito di una possibile perdita di dati.
            // Quarta volta che va corretto a mano: 20260811130532 (#3658), 20260811224354 (#3683),
            // 20260812035720 (#3698).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina la colonna, non un meccanismo che la valorizzi: tornando indietro il token
            // resterebbe NULL come prima di questa migration. È esattamente lo stato precedente.
            // Nessun DropColumn per `xmin`, per la ragione detta nell'Up(): non è stata aggiunta.
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "bgg_tos_hashes",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}
