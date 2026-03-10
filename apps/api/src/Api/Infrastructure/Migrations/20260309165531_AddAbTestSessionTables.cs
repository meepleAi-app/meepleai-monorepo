using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAbTestSessionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ab_test_sessions",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    query = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    knowledge_base_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ab_test_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ab_test_variants",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ab_test_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    response = table.Column<string>(type: "text", nullable: true),
                    tokens_used = table.Column<int>(type: "integer", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    failed = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    evaluator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    eval_accuracy = table.Column<int>(type: "integer", nullable: true),
                    eval_completeness = table.Column<int>(type: "integer", nullable: true),
                    eval_clarity = table.Column<int>(type: "integer", nullable: true),
                    eval_tone = table.Column<int>(type: "integer", nullable: true),
                    eval_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    eval_evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ab_test_variants", x => x.id);
                    table.ForeignKey(
                        name: "FK_ab_test_variants_ab_test_sessions_ab_test_session_id",
                        column: x => x.ab_test_session_id,
                        principalSchema: "knowledge_base",
                        principalTable: "ab_test_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ab_test_sessions_created_at",
                schema: "knowledge_base",
                table: "ab_test_sessions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_ab_test_sessions_created_by",
                schema: "knowledge_base",
                table: "ab_test_sessions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_ab_test_sessions_status",
                schema: "knowledge_base",
                table: "ab_test_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ab_test_variants_ab_test_session_id",
                schema: "knowledge_base",
                table: "ab_test_variants",
                column: "ab_test_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_ab_test_variants_model_id",
                schema: "knowledge_base",
                table: "ab_test_variants",
                column: "model_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ab_test_variants",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "ab_test_sessions",
                schema: "knowledge_base");
        }
    }
}
