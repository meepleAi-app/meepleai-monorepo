using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "correlation_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "correlation_id",
                table: "email_queue_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_correlation_id",
                table: "notifications",
                column: "correlation_id",
                filter: "correlation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_email_queue_items_correlation_id",
                table: "email_queue_items",
                column: "correlation_id",
                filter: "correlation_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_correlation_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_email_queue_items_correlation_id",
                table: "email_queue_items");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "email_queue_items");
        }
    }
}
