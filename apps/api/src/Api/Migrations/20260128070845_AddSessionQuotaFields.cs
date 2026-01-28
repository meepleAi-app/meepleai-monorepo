using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionQuotaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DurationMinutes",
                table: "game_sessions",
                newName: "duration_minutes");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "GameSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_CreatedByUserId_Status",
                table: "GameSessions",
                columns: new[] { "CreatedByUserId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_GameSessions_users_CreatedByUserId",
                table: "GameSessions",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameSessions_users_CreatedByUserId",
                table: "GameSessions");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_CreatedByUserId_Status",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "GameSessions");

            migrationBuilder.RenameColumn(
                name: "duration_minutes",
                table: "game_sessions",
                newName: "DurationMinutes");
        }
    }
}
