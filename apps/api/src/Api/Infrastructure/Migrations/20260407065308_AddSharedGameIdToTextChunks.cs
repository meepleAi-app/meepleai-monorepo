using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedGameIdToTextChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SharedGameId",
                table: "text_chunks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_SharedGameId",
                table: "text_chunks",
                column: "SharedGameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_text_chunks_SharedGameId",
                table: "text_chunks");

            migrationBuilder.DropColumn(
                name: "SharedGameId",
                table: "text_chunks");
        }
    }
}
