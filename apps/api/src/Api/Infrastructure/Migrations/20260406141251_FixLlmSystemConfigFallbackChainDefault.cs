using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixLlmSystemConfigFallbackChainDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FallbackChainJson",
                schema: "SystemConfiguration",
                table: "LlmSystemConfigs",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FallbackChainJson",
                schema: "SystemConfiguration",
                table: "LlmSystemConfigs",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'[]'::jsonb");
        }
    }
}
