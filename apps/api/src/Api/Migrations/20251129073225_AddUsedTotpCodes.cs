using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable MA0048 // File name must match type name - EF Core migration

namespace Api.Migrations
{
    /// <inheritdoc />
    internal partial class AddUsedTotpCodes : Migration
    {
        private static readonly string[] UserCodeExpiryIndexColumns = { "UserId", "CodeHash", "ExpiresAt" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "used_totp_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TimeStep = table.Column<long>(type: "bigint", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_used_totp_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_used_totp_codes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_used_totp_codes_expiry",
                table: "used_totp_codes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_used_totp_codes_user_code_expiry",
                table: "used_totp_codes",
                columns: UserCodeExpiryIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "used_totp_codes");
        }
    }
}
