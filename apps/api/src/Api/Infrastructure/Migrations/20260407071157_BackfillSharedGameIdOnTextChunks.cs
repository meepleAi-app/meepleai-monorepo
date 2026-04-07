using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillSharedGameIdOnTextChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill SharedGameId on text_chunks from pdf_documents for shared game PDFs
            migrationBuilder.Sql("""
                UPDATE text_chunks tc
                SET "SharedGameId" = pd."SharedGameId"
                FROM pdf_documents pd
                WHERE tc."PdfDocumentId" = pd."Id"
                  AND pd."SharedGameId" IS NOT NULL
                  AND tc."SharedGameId" IS NULL;
            """);

            // Fix GameId for text_chunks that have Guid.Empty (the original bug).
            // SharedGame PDFs were indexed with GameId = '00000000-...' instead of the SharedGameId.
            migrationBuilder.Sql("""
                UPDATE text_chunks tc
                SET "GameId" = pd."SharedGameId"
                FROM pdf_documents pd
                WHERE tc."PdfDocumentId" = pd."Id"
                  AND pd."SharedGameId" IS NOT NULL
                  AND (tc."GameId" IS NULL OR tc."GameId" = '00000000-0000-0000-0000-000000000000');
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — data backfill is not reversible
        }
    }
}
