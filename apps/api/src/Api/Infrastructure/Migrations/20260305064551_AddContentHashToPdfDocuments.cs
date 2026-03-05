using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentHashToPdfDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                table: "pdf_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_content_hash_game_id",
                table: "pdf_documents",
                columns: new[] { "content_hash", "GameId" });

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_content_hash_private_game_id",
                table: "pdf_documents",
                columns: new[] { "content_hash", "PrivateGameId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pdf_documents_content_hash_game_id",
                table: "pdf_documents");

            migrationBuilder.DropIndex(
                name: "ix_pdf_documents_content_hash_private_game_id",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "content_hash",
                table: "pdf_documents");
        }
    }
}
