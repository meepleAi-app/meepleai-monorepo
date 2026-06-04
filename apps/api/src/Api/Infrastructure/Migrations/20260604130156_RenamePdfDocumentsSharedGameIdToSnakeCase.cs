using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamePdfDocumentsSharedGameIdToSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #1885: align pdf_documents.SharedGameId with BC SharedGameCatalog convention.
            //
            // The EntityConfig was missing HasColumnName("shared_game_id"), so EF created the
            // column as PascalCase. Migration 20260603120937 then assumed snake_case in its
            // backfill SQL and broke fresh-DB startup.
            //
            // Snapshot drift: the snapshot declares a FK to shared_games, but migration
            // 20260419155458_DropPdfAndCollectionGameId only dropped the legacy FK to games
            // and never recreated one to shared_games. So the physical FK
            // "FK_pdf_documents_shared_games_SharedGameId" does NOT exist on any DB.
            //
            // We use raw SQL with IF EXISTS guards so the migration is robust to any
            // actual DB state. The final state matches both the snapshot (FK present) and
            // the convention (snake_case column + index + constraint name).
            migrationBuilder.Sql(@"
                ALTER TABLE pdf_documents
                    DROP CONSTRAINT IF EXISTS ""FK_pdf_documents_shared_games_SharedGameId"";

                ALTER TABLE pdf_documents
                    RENAME COLUMN ""SharedGameId"" TO shared_game_id;

                ALTER INDEX ""IX_pdf_documents_SharedGameId_UploadedAt""
                    RENAME TO ix_pdf_documents_shared_game_id_uploaded_at;

                ALTER TABLE pdf_documents
                    ADD CONSTRAINT ""FK_pdf_documents_shared_games_shared_game_id""
                    FOREIGN KEY (shared_game_id)
                    REFERENCES shared_games (id)
                    ON DELETE CASCADE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE pdf_documents
                    DROP CONSTRAINT IF EXISTS ""FK_pdf_documents_shared_games_shared_game_id"";

                ALTER INDEX ix_pdf_documents_shared_game_id_uploaded_at
                    RENAME TO ""IX_pdf_documents_SharedGameId_UploadedAt"";

                ALTER TABLE pdf_documents
                    RENAME COLUMN shared_game_id TO ""SharedGameId"";
            ");
        }
    }
}
