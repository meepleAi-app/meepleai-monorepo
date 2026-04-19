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
            // ========= pdf_documents =========

            // 1a. Backfill SharedGameId
            migrationBuilder.Sql(@"
                UPDATE pdf_documents p
                SET ""SharedGameId"" = g.""SharedGameId""
                FROM games g
                WHERE p.""GameId"" = g.""Id""
                  AND p.""SharedGameId"" IS NULL
                  AND g.""SharedGameId"" IS NOT NULL;
            ");

            // 1b. Drop FK + indices + column
            migrationBuilder.DropForeignKey(
                name: "FK_pdf_documents_games_GameId",
                table: "pdf_documents");

            migrationBuilder.DropIndex(
                name: "IX_pdf_documents_GameId_UploadedAt",
                table: "pdf_documents");

            migrationBuilder.DropIndex(
                name: "ix_pdf_documents_content_hash_game_id",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "GameId",
                table: "pdf_documents");

            // 1c. New indices on SharedGameId
            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_SharedGameId_UploadedAt",
                table: "pdf_documents",
                columns: new[] { "SharedGameId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_content_hash_shared_game_id",
                table: "pdf_documents",
                columns: new[] { "ContentHash", "SharedGameId" });

            // ========= document_collections (C1) =========

            // 2a. Add nullable SharedGameId column (backfill target)
            migrationBuilder.AddColumn<Guid>(
                name: "SharedGameId",
                table: "document_collections",
                type: "uuid",
                nullable: true);

            // 2b. Backfill from games.SharedGameId
            migrationBuilder.Sql(@"
                UPDATE document_collections c
                SET ""SharedGameId"" = g.""SharedGameId""
                FROM games g
                WHERE c.""GameId"" = g.""Id""
                  AND g.""SharedGameId"" IS NOT NULL;
            ");

            // 2c. Hard-cut orphans (Q2=D): collections without shared_game mapping are deleted
            //     Cascade rule applies — children (PDFs in collection) lose CollectionId (SetNull per PdfDocumentEntityConfiguration).
            migrationBuilder.Sql(@"
                DELETE FROM document_collections
                WHERE ""SharedGameId"" IS NULL;
            ");

            // 2d. Drop FK + index + column
            migrationBuilder.DropForeignKey(
                name: "FK_document_collections_games_GameId",
                table: "document_collections");

            migrationBuilder.DropIndex(
                name: "IX_document_collections_GameId",
                table: "document_collections");

            migrationBuilder.DropColumn(
                name: "GameId",
                table: "document_collections");

            // 2e. Make SharedGameId required (after backfill + orphan cleanup)
            migrationBuilder.AlterColumn<Guid>(
                name: "SharedGameId",
                table: "document_collections",
                type: "uuid",
                nullable: false);

            // 2f. FK + index on SharedGameId (Cascade per spec Q5=A)
            migrationBuilder.CreateIndex(
                name: "IX_document_collections_SharedGameId",
                table: "document_collections",
                column: "SharedGameId");

            migrationBuilder.AddForeignKey(
                name: "FK_document_collections_shared_games_SharedGameId",
                table: "document_collections",
                column: "SharedGameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
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
                columns: new[] { "ContentHash", "GameId" });

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
