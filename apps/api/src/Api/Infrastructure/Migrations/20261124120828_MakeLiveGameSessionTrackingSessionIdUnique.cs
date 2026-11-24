using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeLiveGameSessionTrackingSessionIdUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_live_game_sessions_tracking_session_id",
                table: "live_game_sessions");

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_tracking_session_id",
                table: "live_game_sessions",
                column: "tracking_session_id",
                unique: true,
                filter: "tracking_session_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_live_game_sessions_tracking_session_id",
                table: "live_game_sessions");

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_tracking_session_id",
                table: "live_game_sessions",
                column: "tracking_session_id",
                filter: "tracking_session_id IS NOT NULL");
        }
    }
}
