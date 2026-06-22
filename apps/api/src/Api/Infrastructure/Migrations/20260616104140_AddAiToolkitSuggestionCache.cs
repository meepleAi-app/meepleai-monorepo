using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiToolkitSuggestionCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_toolkit_suggestion_cache",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggestion_json = table.Column<string>(type: "text", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kb_version = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_toolkit_suggestion_cache", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_toolkit_suggestion_cache_generated_at",
                table: "ai_toolkit_suggestion_cache",
                column: "generated_at");

            migrationBuilder.CreateIndex(
                name: "UX_ai_toolkit_suggestion_cache_game_id",
                table: "ai_toolkit_suggestion_cache",
                column: "game_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_toolkit_suggestion_cache");
        }
    }
}
