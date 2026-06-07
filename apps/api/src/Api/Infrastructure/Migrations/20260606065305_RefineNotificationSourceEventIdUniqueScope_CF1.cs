using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefineNotificationSourceEventIdUniqueScope_CF1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_notifications_source_event_id",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "UX_notifications_user_source_event_id",
                table: "notifications",
                columns: new[] { "user_id", "source_event_id" },
                unique: true,
                filter: "source_event_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_notifications_user_source_event_id",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "UX_notifications_source_event_id",
                table: "notifications",
                column: "source_event_id",
                unique: true,
                filter: "source_event_id IS NOT NULL");
        }
    }
}
