using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reference_questions",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    expected_answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    question_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    out_of_corpus = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    in_gate = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reference_questions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reference_questions_code",
                schema: "knowledge_base",
                table: "reference_questions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reference_questions_shared_game_id",
                schema: "knowledge_base",
                table: "reference_questions",
                column: "shared_game_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reference_questions",
                schema: "knowledge_base");
        }
    }
}
