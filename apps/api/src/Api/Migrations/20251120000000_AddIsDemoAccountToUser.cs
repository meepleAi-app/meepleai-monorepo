using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDemoAccountToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDemoAccount",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Mark existing demo users as demo accounts (case-insensitive)
            migrationBuilder.Sql(@"
                UPDATE users
                SET ""IsDemoAccount"" = true
                WHERE LOWER(""Email"") IN ('admin@meepleai.dev', 'editor@meepleai.dev', 'user@meepleai.dev')
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDemoAccount",
                table: "users");
        }
    }
}
