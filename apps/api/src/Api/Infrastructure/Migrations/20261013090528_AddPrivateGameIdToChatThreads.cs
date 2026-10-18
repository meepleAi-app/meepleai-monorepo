using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateGameIdToChatThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrivateGameId",
                table: "ChatThreads",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_PrivateGameId",
                table: "ChatThreads",
                column: "PrivateGameId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatThreads_private_games_PrivateGameId",
                table: "ChatThreads",
                column: "PrivateGameId",
                principalTable: "private_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatThreads_private_games_PrivateGameId",
                table: "ChatThreads");

            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_PrivateGameId",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "PrivateGameId",
                table: "ChatThreads");
        }
    }
}
