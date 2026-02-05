using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateGamesSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add new columns first (keeping GameId for data migration)
            migrationBuilder.AddColumn<Guid>(
                name: "private_game_id",
                table: "user_library_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shared_game_id",
                table: "user_library_entries",
                type: "uuid",
                nullable: true);

            // Step 2: Migrate existing GameId data to shared_game_id
            migrationBuilder.Sql(
                @"UPDATE user_library_entries
                  SET shared_game_id = ""GameId""
                  WHERE ""GameId"" IS NOT NULL AND ""GameId"" != '00000000-0000-0000-0000-000000000000'");

            // Step 3: Drop old FK and indexes on GameId
            migrationBuilder.DropForeignKey(
                name: "FK_user_library_entries_shared_games_GameId",
                table: "user_library_entries");

            migrationBuilder.DropIndex(
                name: "IX_user_library_entries_GameId",
                table: "user_library_entries");

            migrationBuilder.DropIndex(
                name: "IX_UserLibraryEntries_UserId_GameId",
                table: "user_library_entries");

            // Step 4: Drop the old GameId column
            migrationBuilder.DropColumn(
                name: "GameId",
                table: "user_library_entries");

            migrationBuilder.CreateTable(
                name: "private_games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bgg_id = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    year_published = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    min_players = table.Column<int>(type: "integer", nullable: false),
                    max_players = table.Column<int>(type: "integer", nullable: false),
                    playing_time_minutes = table.Column<int>(type: "integer", nullable: true),
                    min_age = table.Column<int>(type: "integer", nullable: true),
                    complexity_rating = table.Column<decimal>(type: "numeric(3,2)", nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    thumbnail_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bgg_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_private_games", x => x.id);
                    table.CheckConstraint("chk_private_games_complexity", "complexity_rating IS NULL OR (complexity_rating >= 1.0 AND complexity_rating <= 5.0)");
                    table.CheckConstraint("chk_private_games_min_age", "min_age IS NULL OR min_age >= 0");
                    table.CheckConstraint("chk_private_games_players", "min_players > 0 AND max_players >= min_players AND max_players <= 100");
                    table.CheckConstraint("chk_private_games_playing_time", "playing_time_minutes IS NULL OR playing_time_minutes > 0");
                    table.CheckConstraint("chk_private_games_year_published", "year_published IS NULL OR (year_published > 1900 AND year_published <= 2100)");
                    table.ForeignKey(
                        name: "FK_private_games_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_library_entries_private_game_id",
                table: "user_library_entries",
                column: "private_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_library_entries_shared_game_id",
                table: "user_library_entries",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId_PrivateGameId",
                table: "user_library_entries",
                columns: new[] { "UserId", "private_game_id" },
                unique: true,
                filter: "private_game_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId_SharedGameId",
                table: "user_library_entries",
                columns: new[] { "UserId", "shared_game_id" },
                unique: true,
                filter: "shared_game_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserLibraryEntry_GameSource",
                table: "user_library_entries",
                sql: "(shared_game_id IS NOT NULL AND private_game_id IS NULL) OR (shared_game_id IS NULL AND private_game_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_private_games_owner_bgg",
                table: "private_games",
                columns: new[] { "owner_id", "bgg_id" },
                unique: true,
                filter: "bgg_id IS NOT NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_private_games_owner_id",
                table: "private_games",
                column: "owner_id",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_private_games_title",
                table: "private_games",
                column: "title",
                filter: "is_deleted = false");

            migrationBuilder.AddForeignKey(
                name: "FK_user_library_entries_private_games_private_game_id",
                table: "user_library_entries",
                column: "private_game_id",
                principalTable: "private_games",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_user_library_entries_shared_games_shared_game_id",
                table: "user_library_entries",
                column: "shared_game_id",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_library_entries_private_games_private_game_id",
                table: "user_library_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_user_library_entries_shared_games_shared_game_id",
                table: "user_library_entries");

            migrationBuilder.DropTable(
                name: "private_games");

            migrationBuilder.DropIndex(
                name: "IX_user_library_entries_private_game_id",
                table: "user_library_entries");

            migrationBuilder.DropIndex(
                name: "IX_user_library_entries_shared_game_id",
                table: "user_library_entries");

            migrationBuilder.DropIndex(
                name: "IX_UserLibraryEntries_UserId_PrivateGameId",
                table: "user_library_entries");

            migrationBuilder.DropIndex(
                name: "IX_UserLibraryEntries_UserId_SharedGameId",
                table: "user_library_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserLibraryEntry_GameSource",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "private_game_id",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "shared_game_id",
                table: "user_library_entries");

            migrationBuilder.AddColumn<Guid>(
                name: "GameId",
                table: "user_library_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_user_library_entries_GameId",
                table: "user_library_entries",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId_GameId",
                table: "user_library_entries",
                columns: new[] { "UserId", "GameId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_user_library_entries_shared_games_GameId",
                table: "user_library_entries",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
