using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcePrivateGameIdToShareRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DeviceFingerprint",
                table: "user_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(88)",
                oldMaxLength: 88,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_private_game_id",
                table: "share_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_share_requests_source_private_game_id",
                table: "share_requests",
                column: "source_private_game_id",
                filter: "source_private_game_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_share_requests_private_games_source_private_game_id",
                table: "share_requests",
                column: "source_private_game_id",
                principalTable: "private_games",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_share_requests_private_games_source_private_game_id",
                table: "share_requests");

            migrationBuilder.DropIndex(
                name: "ix_share_requests_source_private_game_id",
                table: "share_requests");

            migrationBuilder.DropColumn(
                name: "source_private_game_id",
                table: "share_requests");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceFingerprint",
                table: "user_sessions",
                type: "character varying(88)",
                maxLength: 88,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
