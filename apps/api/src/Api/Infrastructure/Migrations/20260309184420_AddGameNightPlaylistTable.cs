using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameNightPlaylistTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_night_playlists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scheduled_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    creator_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_shared = table.Column<bool>(type: "boolean", nullable: false),
                    games_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_night_playlists", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_night_playlists_creator_user_id",
                table: "game_night_playlists",
                column: "creator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_night_playlists_share_token",
                table: "game_night_playlists",
                column: "share_token",
                unique: true,
                filter: "share_token IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_night_playlists");
        }
    }
}
