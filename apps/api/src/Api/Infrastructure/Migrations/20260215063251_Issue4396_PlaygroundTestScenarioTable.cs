using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores - EF Core migration naming convention
    public partial class Issue4396_PlaygroundTestScenarioTable : Migration
#pragma warning restore CA1707
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "playground_test_scenarios",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    expected_outcome = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    agent_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    messages = table.Column<string>(type: "jsonb", nullable: false),
                    tags = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playground_test_scenarios", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_agent_definition_id",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "agent_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_category",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_created_at",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_created_by",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_is_active",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_playground_test_scenarios_name",
                schema: "knowledge_base",
                table: "playground_test_scenarios",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playground_test_scenarios",
                schema: "knowledge_base");
        }
    }
}
