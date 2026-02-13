using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores - Migration name follows EF convention
    public partial class Issue4220_NotificationPreferences : Migration
#pragma warning restore CA1707
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailOnDocumentReady = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EmailOnDocumentFailed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EmailOnRetryAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PushOnDocumentReady = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PushOnDocumentFailed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PushOnRetryAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    InAppOnDocumentReady = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InAppOnDocumentFailed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InAppOnRetryAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_UserId",
                table: "notification_preferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_preferences");
        }
    }
}
