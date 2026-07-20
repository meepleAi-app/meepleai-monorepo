using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSharedGameBggIdSoftDeleteIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shared_games_bgg_id",
                table: "shared_games");

            migrationBuilder.CreateIndex(
                name: "ix_shared_games_bgg_id",
                table: "shared_games",
                column: "bgg_id",
                unique: true,
                filter: "bgg_id IS NOT NULL AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shared_games_bgg_id",
                table: "shared_games");

            migrationBuilder.CreateIndex(
                name: "ix_shared_games_bgg_id",
                table: "shared_games",
                column: "bgg_id",
                unique: true,
                filter: "bgg_id IS NOT NULL");
        }
    }
}
