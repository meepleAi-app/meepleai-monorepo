using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    internal partial class AddEmailRecipientsToAdminReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email_recipients",
                table: "admin_reports",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]"); // ISSUE-918: Empty array default for backward compatibility
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email_recipients",
                table: "admin_reports");
        }
    }
}
