using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Forward-only data backfill: populates SharedGameId on pdf_documents,
    /// vector_documents, and text_chunks for rows that were created via the
    /// legacy upload path (which linked only to the games table by GameId).
    /// Idempotent: only updates NULL columns.
    /// </summary>
    public partial class BackfillSharedGameIdFromGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE pdf_documents pd
                SET ""SharedGameId"" = g.""SharedGameId""
                FROM games g
                WHERE pd.""GameId"" = g.""Id""
                  AND pd.""SharedGameId"" IS NULL
                  AND g.""SharedGameId"" IS NOT NULL;

                UPDATE vector_documents vd
                SET shared_game_id = pd.""SharedGameId""
                FROM pdf_documents pd
                WHERE vd.""PdfDocumentId"" = pd.""Id""
                  AND vd.shared_game_id IS NULL
                  AND pd.""SharedGameId"" IS NOT NULL;

                UPDATE text_chunks tc
                SET ""SharedGameId"" = pd.""SharedGameId""
                FROM pdf_documents pd
                WHERE tc.""PdfDocumentId"" = pd.""Id""
                  AND tc.""SharedGameId"" IS NULL
                  AND pd.""SharedGameId"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only: the backfill is non-destructive (only populates NULL columns).
            // A rollback is a no-op; data integrity is preserved.
        }
    }
}
