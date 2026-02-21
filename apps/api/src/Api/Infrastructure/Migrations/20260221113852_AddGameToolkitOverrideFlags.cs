using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameToolkitOverrideFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameToolkits_GameId_Version",
                table: "GameToolkits");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "GameToolkits",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "OverridesDiceSet",
                table: "GameToolkits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OverridesScoreboard",
                table: "GameToolkits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OverridesTurnOrder",
                table: "GameToolkits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PrivateGameId",
                table: "GameToolkits",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_GameId_Version",
                table: "GameToolkits",
                columns: new[] { "GameId", "Version" },
                unique: true,
                filter: "\"GameId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_PrivateGameId",
                table: "GameToolkits",
                column: "PrivateGameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_PrivateGameId_Version",
                table: "GameToolkits",
                columns: new[] { "PrivateGameId", "Version" },
                unique: true,
                filter: "\"PrivateGameId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_GameToolkits_private_games_PrivateGameId",
                table: "GameToolkits",
                column: "PrivateGameId",
                principalTable: "private_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameToolkits_private_games_PrivateGameId",
                table: "GameToolkits");

            migrationBuilder.DropIndex(
                name: "IX_GameToolkits_GameId_Version",
                table: "GameToolkits");

            migrationBuilder.DropIndex(
                name: "IX_GameToolkits_PrivateGameId",
                table: "GameToolkits");

            migrationBuilder.DropIndex(
                name: "IX_GameToolkits_PrivateGameId_Version",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "OverridesDiceSet",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "OverridesScoreboard",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "OverridesTurnOrder",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "PrivateGameId",
                table: "GameToolkits");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "GameToolkits",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_GameId_Version",
                table: "GameToolkits",
                columns: new[] { "GameId", "Version" },
                unique: true);
        }
    }
}
