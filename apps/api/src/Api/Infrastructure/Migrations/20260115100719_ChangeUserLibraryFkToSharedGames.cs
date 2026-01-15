using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUserLibraryFkToSharedGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_library_entries_games_GameId",
                table: "user_library_entries");

            migrationBuilder.AddForeignKey(
                name: "FK_user_library_entries_shared_games_GameId",
                table: "user_library_entries",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_library_entries_shared_games_GameId",
                table: "user_library_entries");

            migrationBuilder.AddForeignKey(
                name: "FK_user_library_entries_games_GameId",
                table: "user_library_entries",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
