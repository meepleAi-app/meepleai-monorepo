using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PhotoBatchUploadXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Safety Gate (#1087, rollback-runbook §8.2): il DROP COLUMN è rollback-safe
            // perché la colonna è già di fatto morta. Nessun trigger la popola
            // (`ef_update_row_version()` rimosso da #2305) e Postgres non valorizza una `bytea`,
            // quindi contiene NULL su ogni riga — letteralmente, da quando
            // 20260524190307_FixPhotoBatchUploadRowVersionNullable l'ha resa nullable.
            migrationBuilder.Sql("-- safe: drop dead bytea concurrency column, NULL on every row since 20260524190307 made it nullable; replaced by the xmin system column (§8.3)");

            // #3651 lotto 8. Misurato prima della conversione:
            //   PhotoBatchUploadXminConcurrencyTests.Update_AfterConcurrentWrite_… → «no exception was thrown»
            //
            // Questa riga ha una storia che spiega il difetto meglio di qualsiasi descrizione.
            // `row_version bytea` era NOT NULL e faceva fallire l'INSERT — lo stesso guasto trovato
            // su ab_test_sessions nel lotto 5 (#3714). La migration 20260524190307 lo risolse
            // rendendo la colonna NULLABLE: il sintomo rumoroso sparì, e al suo posto restò il
            // guasto silenzioso di #3651, cioè una protezione dichiarata che non protegge.
            // Un guasto scambiato per un altro, e il commento di allora indicava
            // UserLibraryEntryEntity come «il pattern che funziona correttamente» — anche quella
            // era rotta allo stesso modo, convertita nel lotto 6 (#3715).
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "photo_batch_uploads");

            // NB: nessun AddColumn per `xmin` — colonna di SISTEMA, `ADD COLUMN xmin` fallirebbe.
            // Ottava volta che va corretto a mano: 20260811130532 (#3658), 20260811224354 (#3683),
            // 20260812035720 (#3698), 20260816120212 (#3710), 20260816150535 (#3714),
            // 20260816153040 (#3715), 20260816162423 (#3716).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina la colonna NULLABLE, cioè lo stato immediatamente precedente — non quello
            // NOT NULL di prima del 24 maggio, che rendeva la tabella non scrivibile.
            // Nessun DropColumn per `xmin`: non è stata aggiunta.
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "photo_batch_uploads",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}
