using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditBudgetTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "daily_credits_used",
                table: "user_token_usage",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_daily_reset",
                table: "user_token_usage",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "last_weekly_reset",
                table: "user_token_usage",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "weekly_credits_used",
                table: "user_token_usage",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "daily_credits_limit",
                table: "token_tiers",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "weekly_credits_limit",
                table: "token_tiers",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "daily_credits_used",
                table: "user_token_usage");

            migrationBuilder.DropColumn(
                name: "last_daily_reset",
                table: "user_token_usage");

            migrationBuilder.DropColumn(
                name: "last_weekly_reset",
                table: "user_token_usage");

            migrationBuilder.DropColumn(
                name: "weekly_credits_used",
                table: "user_token_usage");

            migrationBuilder.DropColumn(
                name: "daily_credits_limit",
                table: "token_tiers");

            migrationBuilder.DropColumn(
                name: "weekly_credits_limit",
                table: "token_tiers");
        }
    }
}
