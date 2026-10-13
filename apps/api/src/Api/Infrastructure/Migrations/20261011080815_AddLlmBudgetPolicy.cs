using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmBudgetPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackgroundQuotaPercent",
                schema: "SystemConfiguration",
                table: "LlmSystemConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "DegradeThresholdPercent",
                schema: "SystemConfiguration",
                table: "LlmSystemConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<int>(
                name: "EvaluationQuotaPercent",
                schema: "SystemConfiguration",
                table: "LlmSystemConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundQuotaPercent",
                schema: "SystemConfiguration",
                table: "LlmSystemConfigs");

            migrationBuilder.DropColumn(
                name: "DegradeThresholdPercent",
                schema: "SystemConfiguration",
                table: "LlmSystemConfigs");

            migrationBuilder.DropColumn(
                name: "EvaluationQuotaPercent",
                schema: "SystemConfiguration",
                table: "LlmSystemConfigs");
        }
    }
}
