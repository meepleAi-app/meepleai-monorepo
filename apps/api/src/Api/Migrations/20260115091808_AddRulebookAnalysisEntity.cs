using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRulebookAnalysisEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rulebook_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    key_mechanics_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    victory_conditions_json = table.Column<string>(type: "jsonb", nullable: true),
                    resources_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    game_phases_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    common_questions_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "1.0"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    source = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    analyzed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rulebook_analyses", x => x.id);
                    table.ForeignKey(
                        name: "FK_rulebook_analyses_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rulebook_analyses_game_pdf_active",
                table: "rulebook_analyses",
                columns: new[] { "shared_game_id", "pdf_document_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_rulebook_analyses_game_pdf_version",
                table: "rulebook_analyses",
                columns: new[] { "shared_game_id", "pdf_document_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rulebook_analyses_pdf_document_id",
                table: "rulebook_analyses",
                column: "pdf_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_rulebook_analyses_shared_game_id",
                table: "rulebook_analyses",
                column: "shared_game_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rulebook_analyses");
        }
    }
}
