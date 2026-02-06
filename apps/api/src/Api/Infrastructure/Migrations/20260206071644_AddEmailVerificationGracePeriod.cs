using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationGracePeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationGracePeriodEndsAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // Data migration: Set 7-day grace period for existing unverified users
            // Issue #3672: Grace period allows existing users time to verify without service disruption
            migrationBuilder.Sql(@"
                UPDATE users
                SET ""VerificationGracePeriodEndsAt"" = NOW() + INTERVAL '7 days'
                WHERE ""EmailVerified"" = false
                  AND ""VerificationGracePeriodEndsAt"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationGracePeriodEndsAt",
                table: "users");
        }
    }
}
