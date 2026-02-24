using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolkitSessionStateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "toolkit_session_states",
                schema: "session_tracking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolkit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    widget_states = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolkit_session_states", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_toolkit_session_states_session_id",
                schema: "session_tracking",
                table: "toolkit_session_states",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "uq_toolkit_session_states_session_toolkit",
                schema: "session_tracking",
                table: "toolkit_session_states",
                columns: new[] { "session_id", "toolkit_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "toolkit_session_states",
                schema: "session_tracking");
        }
    }
}
