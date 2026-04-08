using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameNightSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_night_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_night_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    play_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    winner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_night_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_night_sessions_game_night_events_game_night_event_id",
                        column: x => x.game_night_event_id,
                        principalTable: "game_night_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_night_sessions_event_play_order",
                table: "game_night_sessions",
                columns: new[] { "game_night_event_id", "play_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_night_sessions_game_id",
                table: "game_night_sessions",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_game_night_sessions_session_id",
                table: "game_night_sessions",
                column: "session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_night_sessions");
        }
    }
}
