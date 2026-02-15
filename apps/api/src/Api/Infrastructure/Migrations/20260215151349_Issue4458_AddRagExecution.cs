using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores - EF Core migration naming convention
    public partial class Issue4458_AddRagExecution : Migration
#pragma warning restore CA1707
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rag_executions",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    query = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    agent_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    agent_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    strategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_playground = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    total_latency_ms = table.Column<int>(type: "integer", nullable: false),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    cache_hit = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    execution_trace = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_executions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rag_executions_agent_definition_id",
                schema: "knowledge_base",
                table: "rag_executions",
                column: "agent_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_rag_executions_created_at",
                schema: "knowledge_base",
                table: "rag_executions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_rag_executions_status",
                schema: "knowledge_base",
                table: "rag_executions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_rag_executions_strategy",
                schema: "knowledge_base",
                table: "rag_executions",
                column: "strategy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rag_executions",
                schema: "knowledge_base");
        }
    }
}
