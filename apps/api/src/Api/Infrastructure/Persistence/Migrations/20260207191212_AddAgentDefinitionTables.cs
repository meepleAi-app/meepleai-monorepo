using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentDefinitionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "knowledge_base");

            migrationBuilder.CreateTable(
                name: "agent_definitions",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    max_tokens = table.Column<int>(type: "integer", nullable: false),
                    temperature = table.Column<float>(type: "real", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    prompts = table.Column<string>(type: "jsonb", nullable: false),
                    tools = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_definitions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_definitions_created_at",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_agent_definitions_is_active",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_agent_definitions_name",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_definitions",
                schema: "knowledge_base");
        }
    }
}
