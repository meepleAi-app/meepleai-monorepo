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
