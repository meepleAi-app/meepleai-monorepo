using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJsonSettingsToAiModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "settings_json",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "usage_json",
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
                name: "settings_json",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");

            migrationBuilder.DropColumn(
                name: "usage_json",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations");
        }
    }
}
