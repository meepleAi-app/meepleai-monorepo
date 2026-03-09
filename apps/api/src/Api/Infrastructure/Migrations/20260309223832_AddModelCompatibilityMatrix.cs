using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModelCompatibilityMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "model_change_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PreviousModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NewModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AffectedStrategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsAutomatic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_change_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "model_compatibility_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Alternatives = table.Column<string[]>(type: "jsonb", nullable: false),
                    ContextWindow = table.Column<int>(type: "integer", nullable: false),
                    Strengths = table.Column<string[]>(type: "jsonb", nullable: false),
                    IsCurrentlyAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_compatibility_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_model_change_logs_AffectedStrategy",
                table: "model_change_logs",
                column: "AffectedStrategy");

            migrationBuilder.CreateIndex(
                name: "IX_model_change_logs_ChangeType",
                table: "model_change_logs",
                column: "ChangeType");

            migrationBuilder.CreateIndex(
                name: "IX_model_change_logs_ModelId",
                table: "model_change_logs",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_model_change_logs_OccurredAt",
                table: "model_change_logs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_model_compatibility_entries_IsCurrentlyAvailable",
                table: "model_compatibility_entries",
                column: "IsCurrentlyAvailable");

            migrationBuilder.CreateIndex(
                name: "IX_model_compatibility_entries_IsDeprecated",
                table: "model_compatibility_entries",
                column: "IsDeprecated");

            migrationBuilder.CreateIndex(
                name: "IX_model_compatibility_entries_ModelId",
                table: "model_compatibility_entries",
                column: "ModelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model_compatibility_entries_Provider",
                table: "model_compatibility_entries",
                column: "Provider");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model_change_logs");

            migrationBuilder.DropTable(
                name: "model_compatibility_entries");
        }
    }
}
