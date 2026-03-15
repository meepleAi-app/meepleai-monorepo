using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRaptorSummariesGameIdForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add composite index on (GameId, TreeLevel) for efficient level-based queries
            migrationBuilder.CreateIndex(
                name: "IX_RaptorSummaries_GameId_TreeLevel",
                table: "RaptorSummaries",
                columns: new[] { "GameId", "TreeLevel" });

            // Add FK constraint from RaptorSummaries.GameId to games.Id
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

            migrationBuilder.DropIndex(
                name: "IX_RaptorSummaries_GameId_TreeLevel",
                table: "RaptorSummaries");
        }
    }
}
