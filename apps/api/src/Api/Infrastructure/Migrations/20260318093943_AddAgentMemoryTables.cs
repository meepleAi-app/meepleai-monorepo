using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentMemoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_memories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    house_rules_json = table.Column<string>(type: "jsonb", nullable: true),
                    custom_setup_json = table.Column<string>(type: "jsonb", nullable: true),
                    notes_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_memories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_memories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    members_json = table.Column<string>(type: "jsonb", nullable: true),
                    preferences_json = table.Column<string>(type: "jsonb", nullable: true),
                    stats_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_memories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "player_memories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    game_stats_json = table.Column<string>(type: "jsonb", nullable: true),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_memories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_memories_game_id_owner_id",
                table: "game_memories",
                columns: new[] { "game_id", "owner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_memories_creator_id",
                table: "group_memories",
                column: "creator_id");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_memories");

            migrationBuilder.DropTable(
                name: "group_memories");

            migrationBuilder.DropTable(
                name: "player_memories");
        }
    }
}
