using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentDefinitionStrategyField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "strategy",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "jsonb",
                nullable: false,
                defaultValue: "{\"Name\":\"HybridSearch\",\"Parameters\":{\"VectorWeight\":0.7,\"KeywordWeight\":0.3,\"TopK\":10,\"MinScore\":0.55}}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "strategy",
                schema: "knowledge_base",
                table: "agent_definitions");
        }
    }
}
