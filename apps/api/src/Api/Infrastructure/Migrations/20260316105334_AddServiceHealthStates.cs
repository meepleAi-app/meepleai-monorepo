using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceHealthStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_health_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    current_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    previous_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    consecutive_successes = table.Column<int>(type: "integer", nullable: false),
                    last_transition_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_description = table.Column<string>(type: "text", nullable: true),
                    tags = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_health_states", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_health_states_service_name",
                table: "service_health_states",
                column: "service_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_health_states");
        }
    }
}
