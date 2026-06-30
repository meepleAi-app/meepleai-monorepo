using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingSessionIdIndexToLiveGameSessions : Migration
    {
        // #2552: adds a partial index on live_game_sessions.tracking_session_id for the cross-BC
        // companion lookups introduced since SP2 (WHERE tracking_session_id = @id).
        //
        // NOTE: `ef migrations add` also re-emitted a CreateTable for live_session_diary_entries
        // because the model snapshot had drifted — the diary table was dropped from the snapshot
        // when 20260629075250_AlignSearchVectorToEnglishFtsConfig was authored in parallel with the
        // diary migration 20260629061044. That table already exists in every database via 061044, so
        // recreating it here is removed; Up()/Down() keep ONLY the index. The regenerated snapshot
        // (Designer + ModelSnapshot) realigns the diary table as a necessary side effect.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_tracking_session_id",
                table: "live_game_sessions",
                column: "tracking_session_id",
                filter: "tracking_session_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_live_game_sessions_tracking_session_id",
                table: "live_game_sessions");
        }
    }
}
