using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores - EF Core migration naming convention
    public partial class Issue4459_AddRagExecutions : Migration
#pragma warning restore CA1707
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "ix_rag_executions_strategy_id",
                table: "rag_executions",
                column: "strategy_id");

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_executed_by_user_id",
                table: "rag_executions",
                column: "executed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_strategy_executed_at",
                table: "rag_executions",
                columns: new[] { "strategy_id", "executed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rag_executions_parent_execution_id",
                table: "rag_executions",
                column: "parent_execution_id",
                filter: "parent_execution_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "rag_executions");
        }
    }
}
