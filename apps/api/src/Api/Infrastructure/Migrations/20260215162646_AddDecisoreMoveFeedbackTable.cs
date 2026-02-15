using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisoreMoveFeedbackTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "decisore_move_feedback",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    quality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    suggestion_followed = table.Column<bool>(type: "boolean", nullable: false),
                    top_suggested_move = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    position_strength = table.Column<double>(type: "double precision", nullable: false),
                    analysis_depth = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decisore_move_feedback", x => x.id);
                    table.ForeignKey(
                        name: "FK_decisore_move_feedback_GameSessions_game_session_id",
                        column: x => x.game_session_id,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_decisore_move_feedback_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_analysis_depth",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "analysis_depth");

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_game_session_id",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "game_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_outcome_suggestion_followed",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                columns: new[] { "outcome", "suggestion_followed" });

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_quality_submitted_at",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                columns: new[] { "quality", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_submitted_at",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_suggestion_followed",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "suggestion_followed");

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_suggestion_id",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "suggestion_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_decisore_move_feedback_user_id",
                schema: "knowledge_base",
                table: "decisore_move_feedback",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "decisore_move_feedback",
                schema: "knowledge_base");
        }
    }
}
