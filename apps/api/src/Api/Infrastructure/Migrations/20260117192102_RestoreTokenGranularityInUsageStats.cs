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

            migrationBuilder.AddColumn<string>(
                name: "PricingJson",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PricingJson",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

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
