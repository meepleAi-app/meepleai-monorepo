using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuietHoursToNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "quiet_hours_end",
                table: "notification_preferences",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "quiet_hours_start",
                table: "notification_preferences",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "time_zone",
                table: "notification_preferences",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "UTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "quiet_hours_end",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "quiet_hours_start",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "time_zone",
                table: "notification_preferences");
        }
    }
}
