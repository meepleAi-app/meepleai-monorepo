using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropN8nWorkflowIntegrationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "n8n_configs");

            migrationBuilder.DropTable(
                name: "workflow_error_logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "n8n_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastTestResult = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WebhookUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_n8n_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_n8n_configs_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_error_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    error_message = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    execution_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    node_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stack_trace = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    workflow_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_error_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_n8n_configs_CreatedByUserId",
                table: "n8n_configs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_n8n_configs_Name",
                table: "n8n_configs",
                column: "Name",
                unique: true);

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
    }
}
