using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInsightFeedbackTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "insight_feedback",
                schema: "administration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insight_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    insight_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_relevant = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insight_feedback", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rag_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pipeline_definition_json = table.Column<string>(type: "jsonb", nullable: false),
                    test_query = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    executed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    total_duration_ms = table.Column<int>(type: "integer", nullable: false),
                    total_tokens_used = table.Column<int>(type: "integer", nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    blocks_executed = table.Column<int>(type: "integer", nullable: false),
                    blocks_failed = table.Column<int>(type: "integer", nullable: false),
                    final_response = table.Column<string>(type: "text", nullable: true),
                    execution_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    events_json = table.Column<string>(type: "jsonb", nullable: false),
                    config_overrides_json = table.Column<string>(type: "jsonb", nullable: true),
                    parent_execution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_rag_executions_rag_executions_parent_execution_id",
                        column: x => x.parent_execution_id,
                        principalTable: "rag_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_insight_feedback_insight_type",
                schema: "administration",
                table: "insight_feedback",
                column: "insight_type");

            migrationBuilder.CreateIndex(
                name: "IX_insight_feedback_insight_type_is_relevant",
                schema: "administration",
                table: "insight_feedback",
                columns: new[] { "insight_type", "is_relevant" });

            migrationBuilder.CreateIndex(
                name: "IX_insight_feedback_submitted_at",
                schema: "administration",
                table: "insight_feedback",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "IX_insight_feedback_user_id",
                schema: "administration",
                table: "insight_feedback",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_insight_feedback_user_id_insight_id",
                schema: "administration",
                table: "insight_feedback",
                columns: new[] { "user_id", "insight_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_executed_by_user_id",
                table: "rag_executions",
                column: "executed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_parent_execution_id",
                table: "rag_executions",
                column: "parent_execution_id",
                filter: "parent_execution_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_strategy_executed_at",
                table: "rag_executions",
                columns: new[] { "strategy_id", "executed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_strategy_id",
                table: "rag_executions",
                column: "strategy_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "insight_feedback",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "rag_executions");
        }
    }
}
