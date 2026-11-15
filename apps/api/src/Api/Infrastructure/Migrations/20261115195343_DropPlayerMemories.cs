using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #4876: contract of <c>player_memories</c>, the per-player game statistics. The expand (PR #5032) retired the
    /// <c>PlayerMemory</c> aggregate, its repository and the endpoint that read it, and left the table mapped but
    /// unused. The release on <c>main</c> still writes it, but no environment runs a release against this schema
    /// (#4428), so the contract does not wait for one, as in #4973. The values of <c>game_stats_json</c> are discarded:
    /// nothing showed them, and <c>Down</c> recreates the table empty.
    /// </summary>
    public partial class DropPlayerMemories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("-- safe: drop player_memories, contract of #4876; no code reads or writes it since the expand (PR #5032), whose remaining mapping leaves in this same change; the release on main still writes it, but no environment runs a release against this schema: deploy-staging.yml is disabled and deploy-production.yml exists only as .disabled (#4428); the game_stats_json values are discarded (release-promotion.md §5)");
            migrationBuilder.DropTable(
                name: "player_memories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_memories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    game_stats_json = table.Column<string>(type: "jsonb", nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_processed_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    processed_session_ids = table.Column<string>(type: "jsonb", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_memories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_player_memories_group_id",
                table: "player_memories",
                column: "group_id",
                filter: "group_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_player_memories_guest_name",
                table: "player_memories",
                column: "guest_name",
                filter: "user_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_player_memories_user_id",
                table: "player_memories",
                column: "user_id",
                filter: "user_id IS NOT NULL");
        }
    }
}
