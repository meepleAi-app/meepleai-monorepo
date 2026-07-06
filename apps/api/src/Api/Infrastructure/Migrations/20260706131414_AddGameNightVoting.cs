using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameNightVoting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "voting_winner_game_id",
                table: "game_night_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "game_night_votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_night_votes", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_night_votes_game_night_events_event_id",
                        column: x => x.event_id,
                        principalTable: "game_night_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_night_votes_event_candidate",
                table: "game_night_votes",
                columns: new[] { "event_id", "candidate_game_id" });

            migrationBuilder.CreateIndex(
                name: "IX_game_night_votes_event_voter_candidate",
                table: "game_night_votes",
                columns: new[] { "event_id", "voter_user_id", "candidate_game_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_night_votes");

            migrationBuilder.DropColumn(
                name: "voting_winner_game_id",
                table: "game_night_events");
        }
    }
}
