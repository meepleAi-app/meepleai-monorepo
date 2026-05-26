using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionBookProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gamebook_session_book_progress",
                schema: "session_tracking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_location = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    history_json = table.Column<string>(type: "jsonb", nullable: false),
                    last_visited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    notes_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gamebook_session_book_progress", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_session_book_progress_campaign_book",
                schema: "session_tracking",
                table: "gamebook_session_book_progress",
                columns: new[] { "campaign_session_id", "game_book_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gamebook_session_book_progress",
                schema: "session_tracking");
        }
    }
}
