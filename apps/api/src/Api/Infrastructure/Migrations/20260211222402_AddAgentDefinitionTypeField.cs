using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentDefinitionTypeField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type_description",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Retrieval-Augmented Generation for general game rules questions");

            migrationBuilder.AddColumn<string>(
                name: "type_value",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "RAG");

            migrationBuilder.CreateIndex(
                name: "ix_agent_definitions_type_value",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "type_value");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_agent_definitions_type_value",
                schema: "knowledge_base",
                table: "agent_definitions");

            migrationBuilder.DropColumn(
                name: "type_description",
                schema: "knowledge_base",
                table: "agent_definitions");

            migrationBuilder.DropColumn(
                name: "type_value",
                schema: "knowledge_base",
                table: "agent_definitions");
        }
    }
}
