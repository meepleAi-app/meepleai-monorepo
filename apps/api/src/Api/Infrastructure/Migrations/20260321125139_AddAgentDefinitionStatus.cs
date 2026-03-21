using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentDefinitionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "status",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Data migration: existing active definitions -> Published (2)
            migrationBuilder.Sql(
                "UPDATE knowledge_base.agent_definitions SET status = 2 WHERE is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_agent_definitions_status",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_agent_definitions_status",
                schema: "knowledge_base",
                table: "agent_definitions");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "knowledge_base",
                table: "agent_definitions");
        }
    }
}
