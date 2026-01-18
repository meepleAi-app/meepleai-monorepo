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
            // Issue #2577: Only add PricingJson here
            // SettingsJson and UsageJson are already created by migration 081858 as settings_json/usage_json
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
        }
    }
}