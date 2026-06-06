using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceEventIdToNotifications_CF1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_event_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_event_id",
                table: "notification_queue_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_notifications_source_event_id",
                table: "notifications",
                column: "source_event_id",
                unique: true,
                filter: "source_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_notification_queue_items_channel_recipient_source_event",
                table: "notification_queue_items",
                columns: new[] { "channel_type", "recipient_user_id", "source_event_id" },
                unique: true,
                filter: "source_event_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_notifications_source_event_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "UX_notification_queue_items_channel_recipient_source_event",
                table: "notification_queue_items");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                table: "notification_queue_items");
        }
    }
}
