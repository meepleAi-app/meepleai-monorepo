using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #4138 — EXPAND step for retiring <c>AgentDefinition.Type</c>: relaxes
    /// <c>type_value</c> / <c>type_description</c> to NULL and drops the index over the first.
    /// The columns themselves stay.
    /// </summary>
    /// <remarks>
    /// This is deliberately NOT a <c>DROP COLUMN</c>. Per the rollback runbook §8.2 that would be
    /// forbidden in the same deploy that stops reading them: the previous code version mapped
    /// both as <c>IsRequired()</c>, so rolling the code back against the new schema would break
    /// every query on this table. And <c>type_value</c> was NOT NULL with no default, so merely
    /// unmapping it would make every INSERT fail with 23502.
    ///
    /// So the columns are kept as nullable shadow properties — in the model, absent from the
    /// aggregate, written as NULL, read by nobody — and the CONTRACT deploy drops them. That one
    /// is the one that needs the <c>-- safe:</c> directive plus the evidence that nothing reads
    /// them; see §8.3 and §8.4.
    ///
    /// The index goes now rather than later: an index is not part of any code contract, so
    /// dropping it cannot break a rollback.
    /// </remarks>
    public partial class RelaxAgentDefinitionTypeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_agent_definitions_type_value",
                schema: "knowledge_base",
                table: "agent_definitions");

            migrationBuilder.AlterColumn<string>(
                name: "type_value",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "type_description",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "type_value",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "type_description",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_definitions_type_value",
                schema: "knowledge_base",
                table: "agent_definitions",
                column: "type_value");
        }
    }
}
