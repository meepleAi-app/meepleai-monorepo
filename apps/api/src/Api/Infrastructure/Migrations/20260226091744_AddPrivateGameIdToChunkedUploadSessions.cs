using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateGameIdToChunkedUploadSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chunked_upload_sessions_games_GameId",
                table: "chunked_upload_sessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "chunked_upload_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "PrivateGameId",
                table: "chunked_upload_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_chunked_upload_sessions_games_GameId",
                table: "chunked_upload_sessions",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chunked_upload_sessions_games_GameId",
                table: "chunked_upload_sessions");

            migrationBuilder.DropColumn(
                name: "PrivateGameId",
                table: "chunked_upload_sessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "chunked_upload_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_chunked_upload_sessions_games_GameId",
                table: "chunked_upload_sessions",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
