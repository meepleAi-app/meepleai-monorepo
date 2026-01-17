using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsAndUsageTrackingToAiModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostPerInputToken",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerOutputToken",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxTokens",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Temperature",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCostUsd",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "TotalRequests",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TotalTokensUsed",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostPerInputToken",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "CostPerOutputToken",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "MaxTokens",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "Temperature",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "TotalCostUsd",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "TotalRequests",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "TotalTokensUsed",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");
        }
    }
}
