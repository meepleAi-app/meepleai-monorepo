using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowErrorLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflow_error_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    execution_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    error_message = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    node_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stack_trace = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_error_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_error_logs_created_at",
                table: "workflow_error_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_error_logs_execution_id",
                table: "workflow_error_logs",
                column: "execution_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_error_logs_workflow_id",
                table: "workflow_error_logs",
                column: "workflow_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_error_logs");
        }
    }
}
