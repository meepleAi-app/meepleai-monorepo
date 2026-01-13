#pragma warning disable MA0048 // File name must match type name - EF Core migration
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <summary>
    /// Issue #2373 Phase 4: Add SharedGameId FK to Games table for SharedGameCatalog integration.
    /// Also removes deprecated GameFAQs table (replaced by SharedGameCatalog.game_faqs).
    /// </summary>
    public partial class AddSharedGameIdToGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove deprecated GameFAQs table (now using SharedGameCatalog.game_faqs)
            migrationBuilder.DropTable(
                name: "GameFAQs");

            // Add SharedGameId FK for SharedGameCatalog integration
            migrationBuilder.AddColumn<Guid>(
                name: "SharedGameId",
                table: "games",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_SharedGameId",
                table: "games",
                column: "SharedGameId");

            migrationBuilder.AddForeignKey(
                name: "FK_games_shared_games_SharedGameId",
                table: "games",
                column: "SharedGameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_games_shared_games_SharedGameId",
                table: "games");

            migrationBuilder.DropIndex(
                name: "IX_Games_SharedGameId",
                table: "games");

            migrationBuilder.DropColumn(
                name: "SharedGameId",
                table: "games");

            migrationBuilder.CreateTable(
                name: "GameFAQs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Answer = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Upvotes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameFAQs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameFAQs_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameFAQs_GameId_Upvotes_CreatedAt",
                table: "GameFAQs",
                columns: new[] { "GameId", "Upvotes", "CreatedAt" });
        }
    }
}