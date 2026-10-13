using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateGameIdToTextChunksAndVectorDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrivateGameId",
                table: "vector_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrivateGameId",
                table: "text_chunks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_PrivateGameId",
                table: "vector_documents",
                column: "PrivateGameId");

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_PrivateGameId",
                table: "text_chunks",
                column: "PrivateGameId");

            migrationBuilder.AddForeignKey(
                name: "FK_text_chunks_private_games_PrivateGameId",
                table: "text_chunks",
                column: "PrivateGameId",
                principalTable: "private_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_vector_documents_private_games_PrivateGameId",
                table: "vector_documents",
                column: "PrivateGameId",
                principalTable: "private_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_text_chunks_private_games_PrivateGameId",
                table: "text_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_vector_documents_private_games_PrivateGameId",
                table: "vector_documents");

            migrationBuilder.DropIndex(
                name: "IX_vector_documents_PrivateGameId",
                table: "vector_documents");

            migrationBuilder.DropIndex(
                name: "IX_text_chunks_PrivateGameId",
                table: "text_chunks");

            migrationBuilder.DropColumn(
                name: "PrivateGameId",
                table: "vector_documents");

            migrationBuilder.DropColumn(
                name: "PrivateGameId",
                table: "text_chunks");
        }
    }
}
