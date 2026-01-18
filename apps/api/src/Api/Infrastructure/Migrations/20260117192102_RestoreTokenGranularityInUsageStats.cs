using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreTokenGranularityInUsageStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #2577: Removed duplicate AddColumn for PricingJson
            // PricingJson is already created by migration 090414_AddJsonbColumnsToAiModelConfiguration
            // This migration only needs to ALTER existing settings_json/usage_json columns

            migrationBuilder.AlterColumn<string>(
                name: "usage_json",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}",
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "settings_json",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}",
                oldClrType: typeof(string),
                oldType: "jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "usage_json",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValue: "{}");

            migrationBuilder.AlterColumn<string>(
                name: "settings_json",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValue: "{}");
        }
    }
}