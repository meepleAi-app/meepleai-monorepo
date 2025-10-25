using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.src.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemConfigurationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_configurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresRestart = table.Column<bool>(type: "boolean", nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PreviousValue = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastToggledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_system_configurations_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_system_configurations_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_Category",
                table: "system_configurations",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_CreatedByUserId",
                table: "system_configurations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_Environment",
                table: "system_configurations",
                column: "Environment");

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_IsActive",
                table: "system_configurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_Key_Environment",
                table: "system_configurations",
                columns: new[] { "Key", "Environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_UpdatedAt",
                table: "system_configurations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_system_configurations_UpdatedByUserId",
                table: "system_configurations",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_configurations");
        }
    }
}
