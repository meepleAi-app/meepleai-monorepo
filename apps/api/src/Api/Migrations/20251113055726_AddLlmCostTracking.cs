using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmCostTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "llm_cost_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_tokens = table.Column<int>(type: "integer", nullable: false),
                    input_cost_usd = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    output_cost_usd = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    total_cost_usd = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    request_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_cost_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_llm_cost_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_llm_cost_logs_created_at",
                table: "llm_cost_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_llm_cost_logs_provider_date",
                table: "llm_cost_logs",
                columns: new[] { "provider", "request_date" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_cost_logs_request_date",
                table: "llm_cost_logs",
                column: "request_date");

            migrationBuilder.CreateIndex(
                name: "ix_llm_cost_logs_role_date",
                table: "llm_cost_logs",
                columns: new[] { "user_role", "request_date" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_cost_logs_user_id",
                table: "llm_cost_logs",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llm_cost_logs");
        }
    }
}
