using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedGamePdfCoverR2Key : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pdf_cover_r2_key",
                table: "shared_games",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            // Issue #1852: backfill from already-generated PDF covers (idempotent).
            // Populates pdf_cover_r2_key for any shared_game that already has a
            // PdfDocument with cover_generation_status = 'Generated' and a non-null
            // cover_r2_key. The sg.pdf_cover_r2_key IS NULL guard makes re-applying safe.
            //
            // Issue #1885: At this point in the migration sequence, pdf_documents.SharedGameId
            // is still PascalCase (EF default — PdfDocumentEntityConfiguration didn't set
            // HasColumnName). The subsequent migration RenamePdfDocumentsSharedGameIdToSnakeCase
            // renames it to shared_game_id. We escape "SharedGameId" here so the UPDATE
            // succeeds in the pre-rename state. Note: shared_games has no updated_at column
            // (the entity uses ModifiedAt → modified_at, only set on user-driven updates).
            migrationBuilder.Sql(@"
                UPDATE shared_games sg
                SET pdf_cover_r2_key = pd.cover_r2_key
                FROM pdf_documents pd
                WHERE pd.""SharedGameId"" = sg.id
                  AND pd.cover_generation_status = 'Generated'
                  AND pd.cover_r2_key IS NOT NULL
                  AND sg.pdf_cover_r2_key IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pdf_cover_r2_key",
                table: "shared_games");
        }
    }
}
