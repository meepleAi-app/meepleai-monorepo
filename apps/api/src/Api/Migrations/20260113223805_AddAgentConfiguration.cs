using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    llm_provider = table.Column<int>(type: "integer", nullable: false),
                    llm_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    agent_mode = table.Column<int>(type: "integer", nullable: false),
                    selected_document_ids_json = table.Column<string>(type: "jsonb", nullable: true),
                    temperature = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 0.7m),
                    max_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 4096),
                    system_prompt_override = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_configurations", x => x.id);
                    table.CheckConstraint("ck_agent_configurations_max_tokens", "max_tokens > 0 AND max_tokens <= 32000");
                    table.CheckConstraint("ck_agent_configurations_temperature", "temperature >= 0.0 AND temperature <= 2.0");
                    table.ForeignKey(
                        name: "FK_agent_configurations_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_configurations_agent_id",
                table: "agent_configurations",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_configurations_current",
                table: "agent_configurations",
                columns: new[] { "agent_id", "is_current" },
                unique: true,
                filter: "is_current = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_configurations");
        }
    }
}
