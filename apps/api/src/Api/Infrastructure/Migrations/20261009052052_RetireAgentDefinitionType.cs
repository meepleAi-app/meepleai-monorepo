using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #4138 — drops <c>type_value</c>, <c>type_description</c> and the index over the
    /// first from <c>knowledge_base.agent_definitions</c>.
    /// </summary>
    /// <remarks>
    /// One system agent, configured by the admin and used by everyone. The agent types never had
    /// an effect on the answer path: <c>AskQuestionQuery</c> carries no agent id and
    /// <c>AskQuestionQueryHandler</c> does not mention <c>AgentDefinition</c> (measures E1-E2 on
    /// the issue), so this was a field that lied.
    ///
    /// ⚠️ <c>Down</c> restores the COLUMNS, not the VALUES: the stored types are lost, and rolling
    /// back leaves every row with the empty-string default. That is acceptable because nothing
    /// read them, but it means this migration is reversible only in shape.
    /// </remarks>
    public partial class RetireAgentDefinitionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type_description",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type_value",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_agent_definitions_type_value",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "type_value");
        }
    }
}
