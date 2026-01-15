using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateGameStateTemplateTable : Migration
    {
        private static readonly string[] IndexColumnsSharedGameIdIsActive = new[] { "shared_game_id", "is_active" };
        private static readonly string[] IndexColumnsSharedGameIdVersion = new[] { "shared_game_id", "version" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_state_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    schema_json = table.Column<string>(type: "jsonb", nullable: true),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "1.0"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    source = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_state_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_state_templates_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_state_templates_shared_game_id",
                table: "game_state_templates",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_state_templates_shared_game_id_is_active",
                table: "game_state_templates",
                columns: IndexColumnsSharedGameIdIsActive);

            migrationBuilder.CreateIndex(
                name: "ix_game_state_templates_shared_game_id_version",
                table: "game_state_templates",
                columns: IndexColumnsSharedGameIdVersion,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_state_templates");
        }
    }
}
