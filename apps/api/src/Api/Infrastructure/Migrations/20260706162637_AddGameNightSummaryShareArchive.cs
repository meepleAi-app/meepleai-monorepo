using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameNightSummaryShareArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "game_night_events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_shared",
                table: "game_night_events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "share_token",
                table: "game_night_events",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_night_events_share_token",
                table: "game_night_events",
                column: "share_token",
                unique: true,
                filter: "share_token IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_game_night_events_share_token",
                table: "game_night_events");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "game_night_events");

            migrationBuilder.DropColumn(
                name: "is_shared",
                table: "game_night_events");

            migrationBuilder.DropColumn(
                name: "share_token",
                table: "game_night_events");
        }
    }
}
