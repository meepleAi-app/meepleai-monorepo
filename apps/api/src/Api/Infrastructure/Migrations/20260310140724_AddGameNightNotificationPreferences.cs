using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameNightNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailOnGameNightInvitation",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailOnGameNightReminder",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "InAppOnGameNightInvitation",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PushOnGameNightInvitation",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PushOnGameNightReminder",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailOnGameNightInvitation",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "EmailOnGameNightReminder",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "InAppOnGameNightInvitation",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "PushOnGameNightInvitation",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "PushOnGameNightReminder",
                table: "notification_preferences");
        }
    }
}
