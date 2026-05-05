using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropPdfAndCollectionGameId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: This migration is idempotent against actual schema state, not just
            // __EFMigrationsHistory. Staging deploy run 24782673236 failed because the FK
            // FK_pdf_documents_games_GameId had been dropped out-of-band (not tracked in
            // migration history) — EF's generated DROP CONSTRAINT without IF EXISTS then
            // raised ERROR 42704. Using raw Sql() with PostgreSQL IF EXISTS / IF NOT EXISTS
            // clauses makes this migration safe to apply against DBs with partial drift.

            // ========= pdf_documents =========

            // 1a. Backfill SharedGameId (guarded: only if GameId column still exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'pdf_documents' AND column_name = 'GameId'
                    ) THEN
                        UPDATE pdf_documents p
                        SET ""SharedGameId"" = g.""SharedGameId""
                        FROM games g
                        WHERE p.""GameId"" = g.""Id""
                          AND p.""SharedGameId"" IS NULL
                          AND g.""SharedGameId"" IS NOT NULL;
                    END IF;
                END $$;
            ");

            // 1b. Drop FK + indices + column (idempotent)
            migrationBuilder.Sql(@"
                ALTER TABLE pdf_documents DROP CONSTRAINT IF EXISTS ""FK_pdf_documents_games_GameId"";
                DROP INDEX IF EXISTS ""IX_pdf_documents_GameId_UploadedAt"";
                DROP INDEX IF EXISTS ix_pdf_documents_content_hash_game_id;
                ALTER TABLE pdf_documents DROP COLUMN IF EXISTS ""GameId"";
            ");

            // 1c. New indices on SharedGameId (idempotent)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_pdf_documents_SharedGameId_UploadedAt""
                    ON pdf_documents (""SharedGameId"", ""UploadedAt"");
                CREATE INDEX IF NOT EXISTS ix_pdf_documents_content_hash_shared_game_id
                    ON pdf_documents (content_hash, ""SharedGameId"");
            ");

            // ========= document_collections (C1) =========

            // 2a. Add nullable SharedGameId column (backfill target; idempotent)
            migrationBuilder.Sql(@"
                ALTER TABLE document_collections ADD COLUMN IF NOT EXISTS ""SharedGameId"" uuid;
            ");

            // 2b. Backfill from games.SharedGameId (guarded: only if GameId column still exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'document_collections' AND column_name = 'GameId'
                    ) THEN
                        UPDATE document_collections c
                        SET ""SharedGameId"" = g.""SharedGameId""
                        FROM games g
                        WHERE c.""GameId"" = g.""Id""
                          AND g.""SharedGameId"" IS NOT NULL;
                    END IF;
                END $$;
            ");

            // 2c. Hard-cut orphans (Q2=D): collections without shared_game mapping are deleted
            //     Cascade rule applies — children (PDFs in collection) lose CollectionId (SetNull per PdfDocumentEntityConfiguration).
            //     Naturally idempotent: re-running after NOT NULL is set finds zero rows.
            migrationBuilder.Sql(@"
                DELETE FROM document_collections
                WHERE ""SharedGameId"" IS NULL;
            ");

            // 2d. Drop FK + index + column (idempotent)
            migrationBuilder.Sql(@"
                ALTER TABLE document_collections DROP CONSTRAINT IF EXISTS ""FK_document_collections_games_GameId"";
                DROP INDEX IF EXISTS ""IX_document_collections_GameId"";
                ALTER TABLE document_collections DROP COLUMN IF EXISTS ""GameId"";
            ");

            // 2e. Make SharedGameId required (after backfill + orphan cleanup)
            //     PostgreSQL SET NOT NULL is naturally idempotent — no error if already NOT NULL.
            migrationBuilder.Sql(@"
                ALTER TABLE document_collections
                ALTER COLUMN ""SharedGameId"" SET NOT NULL;
            ");

            // 2f. FK + index on SharedGameId (Cascade per spec Q5=A; idempotent)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_document_collections_SharedGameId""
                    ON document_collections (""SharedGameId"");

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_document_collections_shared_games_SharedGameId'
                    ) THEN
                        ALTER TABLE document_collections
                            ADD CONSTRAINT ""FK_document_collections_shared_games_SharedGameId""
                            FOREIGN KEY (""SharedGameId"") REFERENCES shared_games (id) ON DELETE CASCADE;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // WARNING: Down is lossy — GameId values NOT restored. Restore from backup if needed.

            // document_collections
            migrationBuilder.DropForeignKey(
                name: "FK_document_collections_shared_games_SharedGameId",
                table: "document_collections");

            migrationBuilder.DropIndex(
                name: "IX_document_collections_SharedGameId",
                table: "document_collections");

            migrationBuilder.AlterColumn<Guid>(
                name: "SharedGameId",
                table: "document_collections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GameId",
                table: "document_collections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_collections_GameId",
                table: "document_collections",
                column: "GameId");

            migrationBuilder.AddForeignKey(
                name: "FK_document_collections_games_GameId",
                table: "document_collections",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // pdf_documents
            migrationBuilder.DropIndex(
                name: "IX_pdf_documents_SharedGameId_UploadedAt",
                table: "pdf_documents");

            migrationBuilder.DropIndex(
                name: "ix_pdf_documents_content_hash_shared_game_id",
                table: "pdf_documents");

            migrationBuilder.AddColumn<Guid>(
                name: "GameId",
                table: "pdf_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_GameId_UploadedAt",
                table: "pdf_documents",
                columns: new[] { "GameId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_content_hash_game_id",
                table: "pdf_documents",
                columns: new[] { "content_hash", "GameId" });

            migrationBuilder.AddForeignKey(
                name: "FK_pdf_documents_games_GameId",
                table: "pdf_documents",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
