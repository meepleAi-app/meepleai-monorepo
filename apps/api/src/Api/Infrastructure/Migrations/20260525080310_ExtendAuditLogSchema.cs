using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtendAuditLogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "after_json",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "before_json",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "impersonated_user_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "step_up_token_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_outbox", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_impersonated_user_id",
                table: "audit_logs",
                column: "impersonated_user_id",
                filter: "\"impersonated_user_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_outbox_status_created_at",
                table: "audit_outbox",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_outbox");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_impersonated_user_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "after_json",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "before_json",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "impersonated_user_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "step_up_token_id",
                table: "audit_logs");
        }
    }
}
