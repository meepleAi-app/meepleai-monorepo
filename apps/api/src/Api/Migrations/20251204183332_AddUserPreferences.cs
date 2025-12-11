using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable MA0048 // File name must match type name - EF Core migration

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DataRetentionDays",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.AddColumn<bool>(
                name: "EmailNotifications",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "users",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "system");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataRetentionDays",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailNotifications",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Theme",
                table: "users");
        }
    }
}
