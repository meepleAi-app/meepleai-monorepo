using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertRulesAndConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    config_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    config_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_encrypted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    alert_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    threshold = table.Column<double>(type: "double precision", nullable: false),
                    threshold_unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alert_configurations_category",
                table: "alert_configurations",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_alert_configurations_config_key",
                table: "alert_configurations",
                column: "config_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alert_configurations_updated_at",
                table: "alert_configurations",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_alert_type",
                table: "alert_rules",
                column: "alert_type");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_created_at",
                table: "alert_rules",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_is_enabled",
                table: "alert_rules",
                column: "is_enabled");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_name",
                table: "alert_rules",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_configurations");

            migrationBuilder.DropTable(
                name: "alert_rules");
        }
    }
}
