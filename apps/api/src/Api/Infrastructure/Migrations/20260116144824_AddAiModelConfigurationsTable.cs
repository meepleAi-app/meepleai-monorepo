using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiModelConfigurationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "SystemConfiguration");

            migrationBuilder.CreateTable(
                name: "AiModelConfigurations",
                schema: "SystemConfiguration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelConfigurations", x => x.Id);
                });

#pragma warning disable CA1861 // Analyzer issue with EF migrations - array is not modified
            migrationBuilder.CreateIndex(
                name: "IX_AiModelConfigurations_IsPrimary_IsActive",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                columns: new[] { "IsPrimary", "IsActive" });
#pragma warning restore CA1861

            migrationBuilder.CreateIndex(
                name: "IX_AiModelConfigurations_ModelId",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                column: "ModelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiModelConfigurations_Priority",
                schema: "SystemConfiguration",
                table: "AiModelConfigurations",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiModelConfigurations",
                schema: "SystemConfiguration");
        }
    }
}
