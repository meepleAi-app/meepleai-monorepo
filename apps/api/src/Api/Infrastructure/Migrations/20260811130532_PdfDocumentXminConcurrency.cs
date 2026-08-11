using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PdfDocumentXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Safety Gate (#1087, rollback-runbook §8.2): il DROP COLUMN qui sotto è
            // rollback-safe perché la colonna è già di fatto morta — contiene NULL su ogni riga da
            // quando #2305 ha rimosso il trigger `ef_update_row_version()` che la popolava, e
            // nessun codice la legge (la proprietà non è esposta da alcun DTO). Non c'è quindi
            // alcun dato da preservare, e il Down() la ricrea identica.
            migrationBuilder.Sql("-- safe: drop dead bytea concurrency column, NULL on every row since #2305 removed its trigger; replaced by the xmin system column (§8.3)");

            // #3651: rimuove la colonna `RowVersion` (bytea), residuo del meccanismo pre-#2305.
            // Era popolata dal trigger `ef_update_row_version()`, rimosso da #2305 quando le altre
            // entità sono passate a xmin: da allora restava NULL e la concorrenza ottimistica su
            // pdf_documents non rilevava alcun conflitto.
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "pdf_documents");

            // NB: nessun AddColumn per `xmin`. È una colonna di SISTEMA di PostgreSQL, presente su
            // ogni tabella: `ADD COLUMN xmin` fallirebbe con «column name "xmin" conflicts with a
            // system column name». Lo scaffold di EF l'aveva generata perché vede una proprietà
            // nuova nel modello e non sa che è di sistema — stessa ragione per cui il comando ha
            // avvertito di una possibile perdita di dati.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina la colonna, non il trigger che la valorizzava: tornando indietro il token
            // resterebbe NULL come prima di questa migration. È esattamente lo stato precedente.
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "pdf_documents",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}
