using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTestResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_test_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    typology_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_override = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    model_used = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    query = table.Column<string>(type: "text", nullable: false),
                    response = table.Column<string>(type: "text", nullable: false),
                    confidence_score = table.Column<double>(type: "double precision", nullable: false),
                    tokens_used = table.Column<int>(type: "integer", nullable: false),
                    cost_estimate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    citations_json = table.Column<string>(type: "jsonb", nullable: true),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    executed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_saved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_test_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_test_results_agent_typologies_typology_id",
                        column: x => x.typology_id,
                        principalTable: "agent_typologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_test_results_users_executed_by",
                        column: x => x.executed_by,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_test_results_executed_at",
                table: "agent_test_results",
                column: "executed_at");

            migrationBuilder.CreateIndex(
                name: "ix_agent_test_results_executed_by",
                table: "agent_test_results",
                column: "executed_by");

            migrationBuilder.CreateIndex(
                name: "ix_agent_test_results_is_saved",
                table: "agent_test_results",
                column: "is_saved");

            migrationBuilder.CreateIndex(
                name: "ix_agent_test_results_typology_executed_at",
                table: "agent_test_results",
                columns: new[] { "typology_id", "executed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_test_results_typology_id",
                table: "agent_test_results",
                column: "typology_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_test_results");
        }
    }
}
