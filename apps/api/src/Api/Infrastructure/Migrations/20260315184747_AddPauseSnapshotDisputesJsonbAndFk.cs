using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPauseSnapshotDisputesJsonbAndFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_pause_snapshots_live_game_sessions_live_game_session_id",
                table: "pause_snapshots",
                column: "live_game_session_id",
                principalTable: "live_game_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pause_snapshots_live_game_sessions_live_game_session_id",
                table: "pause_snapshots");
        }
    }
}
