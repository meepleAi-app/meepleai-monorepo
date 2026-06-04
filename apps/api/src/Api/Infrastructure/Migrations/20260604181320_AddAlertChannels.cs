using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_channels",
                columns: table => new
                {
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_tested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_test_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    last_test_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_channels", x => x.type);
                    table.CheckConstraint("ck_alert_channels_type", "type IN ('email', 'slack')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_channels");
        }
    }
}
