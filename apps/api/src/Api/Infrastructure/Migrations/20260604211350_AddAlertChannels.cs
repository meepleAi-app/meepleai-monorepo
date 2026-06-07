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
            // Issue #1674 fix: removed duplicated CreateTable/CreateIndex for
            // enrichment_attempts + enrichment_queue_entries — already created by
            // 20260604154416_Add_EnrichmentQueueAndAttempts (4 h earlier).
            // The duplicate caused 42P07 "relation already exists" during
            // MigrateAsync on fresh Testcontainers DBs, breaking all KnowledgeBase
            // integration tests in main-dev baseline.
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
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
