using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolStateEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tool_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolkit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tool_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tool_type = table.Column<int>(type: "integer", nullable: false),
                    state_data_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_states", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tool_states_session_id",
                table: "tool_states",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_tool_states_session_tool_name",
                table: "tool_states",
                columns: new[] { "session_id", "tool_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tool_states_toolkit_id",
                table: "tool_states",
                column: "toolkit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tool_states");
        }
    }
}
