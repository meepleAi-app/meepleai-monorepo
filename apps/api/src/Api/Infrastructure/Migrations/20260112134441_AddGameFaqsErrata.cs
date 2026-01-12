using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameFaqsErrata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_errata",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    page_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    published_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_errata", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_errata_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_faqs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    answer = table.Column<string>(type: "text", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_faqs", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_faqs_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_errata_published_date",
                table: "game_errata",
                column: "published_date",
                descending: Array.Empty<bool>());

            migrationBuilder.CreateIndex(
                name: "ix_game_errata_shared_game_id",
                table: "game_errata",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_faqs_order",
                table: "game_faqs",
                columns: new[] { "shared_game_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_game_faqs_shared_game_id",
                table: "game_faqs",
                column: "shared_game_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_errata");

            migrationBuilder.DropTable(
                name: "game_faqs");
        }
    }
}
