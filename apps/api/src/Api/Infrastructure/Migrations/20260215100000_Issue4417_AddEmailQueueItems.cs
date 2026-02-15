using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores - EF Core migration naming convention
    public partial class Issue4417_AddEmailQueueItems : Migration
#pragma warning restore CA1707
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_queue_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    html_body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    max_retries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_queue_items", x => x.id);
                });

            // Index for batch pickup: pending emails and failed emails ready for retry
            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_status_next_retry_at",
                table: "email_queue_items",
                columns: new[] { "status", "next_retry_at" });

            // Index for user email history (paginated, newest first)
            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_user_id_created_at",
                table: "email_queue_items",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            // Index for status monitoring
            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_status",
                table: "email_queue_items",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_queue_items");
        }
    }
}
