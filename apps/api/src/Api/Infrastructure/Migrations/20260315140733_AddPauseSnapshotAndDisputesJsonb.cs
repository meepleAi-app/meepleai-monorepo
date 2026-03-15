using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPauseSnapshotAndDisputesJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "disputes_json",
                table: "live_game_sessions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pause_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_turn = table.Column<int>(type: "integer", nullable: false),
                    current_phase = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    player_scores_json = table.Column<string>(type: "jsonb", nullable: false),
                    attachment_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    disputes_json = table.Column<string>(type: "jsonb", nullable: false),
                    agent_conversation_summary = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    game_state_json = table.Column<string>(type: "jsonb", nullable: true),
                    saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    saved_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_auto_save = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pause_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaptorSummaries_GameId",
                table: "RaptorSummaries",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "ix_pause_snapshots_live_game_session_id",
                table: "pause_snapshots",
                column: "live_game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_pause_snapshots_session_is_auto_save",
                table: "pause_snapshots",
                columns: new[] { "live_game_session_id", "is_auto_save" });

            migrationBuilder.CreateIndex(
                name: "ix_pause_snapshots_session_saved_at",
                table: "pause_snapshots",
                columns: new[] { "live_game_session_id", "saved_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_RaptorSummaries_games_GameId",
                table: "RaptorSummaries",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RaptorSummaries_games_GameId",
                table: "RaptorSummaries");

            migrationBuilder.DropTable(
                name: "pause_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_RaptorSummaries_GameId",
                table: "RaptorSummaries");

            migrationBuilder.DropColumn(
                name: "disputes_json",
                table: "live_game_sessions");
        }
    }
}
