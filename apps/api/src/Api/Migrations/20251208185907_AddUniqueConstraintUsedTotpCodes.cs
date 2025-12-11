using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintUsedTotpCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_used_totp_codes_user_code_expiry",
                table: "used_totp_codes");

            migrationBuilder.CreateIndex(
                name: "ix_used_totp_codes_user_code_unique",
                table: "used_totp_codes",
                columns: new[] { "UserId", "CodeHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_used_totp_codes_user_code_unique",
                table: "used_totp_codes");

            migrationBuilder.CreateIndex(
                name: "ix_used_totp_codes_user_code_expiry",
                table: "used_totp_codes",
                columns: new[] { "UserId", "CodeHash", "ExpiresAt" });
        }
    }
}
