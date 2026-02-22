using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmRequestLogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "llm_request_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    request_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Manual"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_tokens = table.Column<int>(type: "integer", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_streaming = table.Column<bool>(type: "boolean", nullable: false),
                    is_free_model = table.Column<bool>(type: "boolean", nullable: false),
                    session_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_request_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_llm_request_logs_expires_at",
                table: "llm_request_logs",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_llm_request_logs_model_requested_at",
                table: "llm_request_logs",
                columns: new[] { "model_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_request_logs_provider",
                table: "llm_request_logs",
                column: "provider");

            migrationBuilder.CreateIndex(
                name: "ix_llm_request_logs_requested_at_source",
                table: "llm_request_logs",
                columns: new[] { "requested_at", "request_source" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_request_logs_user_requested_at",
                table: "llm_request_logs",
                columns: new[] { "user_id", "requested_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llm_request_logs");
        }
    }
}
