using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickQuestionsTable : Migration
    {
        private static readonly string[] ActiveIndexColumns = new[] { "shared_game_id", "is_active" };
        private static readonly string[] OrderIndexColumns = new[] { "shared_game_id", "display_order" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quick_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    emoji = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_generated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quick_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_quick_questions_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quick_questions_active",
                table: "quick_questions",
                columns: ActiveIndexColumns);

            migrationBuilder.CreateIndex(
                name: "ix_quick_questions_order",
                table: "quick_questions",
                columns: OrderIndexColumns);

            migrationBuilder.CreateIndex(
                name: "ix_quick_questions_shared_game_id",
                table: "quick_questions",
                column: "shared_game_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quick_questions");
        }
    }
}
