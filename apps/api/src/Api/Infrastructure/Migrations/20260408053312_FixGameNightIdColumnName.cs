using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixGameNightIdColumnName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GameNightId",
                table: "session_events",
                newName: "game_night_id");

            migrationBuilder.RenameIndex(
                name: "IX_session_events_GameNightId",
                table: "session_events",
                newName: "IX_session_events_game_night_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "game_night_id",
                table: "session_events",
                newName: "GameNightId");

            migrationBuilder.RenameIndex(
                name: "IX_session_events_game_night_id",
                table: "session_events",
                newName: "IX_session_events_GameNightId");
        }
    }
}
