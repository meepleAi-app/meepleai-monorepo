using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJsonbColumnsToAiModelConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PricingJson",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "SettingsJson",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "UsageJson",
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

            migrationBuilder.DropColumn(
                name: "SettingsJson",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "UsageJson",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");
        }
    }
}
