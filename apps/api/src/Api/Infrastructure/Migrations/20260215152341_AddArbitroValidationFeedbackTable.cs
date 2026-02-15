using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArbitroValidationFeedbackTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arbitro_validation_feedback",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    validation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    accuracy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ai_decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ai_confidence = table.Column<double>(type: "double precision", nullable: false),
                    had_conflicts = table.Column<bool>(type: "boolean", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arbitro_validation_feedback", x => x.id);
                    table.ForeignKey(
                        name: "FK_arbitro_validation_feedback_GameSessions_game_session_id",
                        column: x => x.game_session_id,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_arbitro_validation_feedback_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_queue_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    html_body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    max_retries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_queue_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_accuracy_submitted_at",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                columns: new[] { "accuracy", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_game_session_id",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                column: "game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_had_conflicts",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                column: "had_conflicts");

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_submitted_at",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_user_id",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_arbitro_validation_feedback_validation_id",
                schema: "knowledge_base",
                table: "arbitro_validation_feedback",
                column: "validation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_status",
                table: "email_queue_items",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_status_next_retry_at",
                table: "email_queue_items",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_user_id_created_at",
                table: "email_queue_items",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arbitro_validation_feedback",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "email_queue_items");
        }
    }
}
