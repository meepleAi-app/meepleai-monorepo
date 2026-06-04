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
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_channels", x => x.type);
                    table.CheckConstraint("ck_alert_channels_type", "type IN ('email', 'slack')");
                });

            migrationBuilder.CreateTable(
                name: "enrichment_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_sync_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_detail = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enrichment_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_enrichment_attempts_catalog_sync_runs_catalog_sync_run_id",
                        column: x => x.catalog_sync_run_id,
                        principalTable: "catalog_sync_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_enrichment_attempts_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "enrichment_queue_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    queued_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_processed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enrichment_queue_entries", x => x.id);
                    table.CheckConstraint("ck_enrichment_queue_entries_priority_range", "priority BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_enrichment_queue_entries_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_enrichment_queue_entries_users_queued_by_user_id",
                        column: x => x.queued_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_enrichment_attempts_catalog_sync_run_id",
                table: "enrichment_attempts",
                column: "catalog_sync_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrichment_attempts_shared_game_attempted_at",
                table: "enrichment_attempts",
                columns: new[] { "shared_game_id", "attempted_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_enrichment_attempts_success_attempted_at",
                table: "enrichment_attempts",
                columns: new[] { "success", "attempted_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_enrichment_queue_entries_pending_listing",
                table: "enrichment_queue_entries",
                columns: new[] { "is_processed", "priority", "queued_at" },
                filter: "is_processed = false");

            migrationBuilder.CreateIndex(
                name: "IX_enrichment_queue_entries_queued_by_user_id",
                table: "enrichment_queue_entries",
                column: "queued_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrichment_queue_entries_shared_game_id",
                table: "enrichment_queue_entries",
                column: "shared_game_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_channels");

            migrationBuilder.DropTable(
                name: "enrichment_attempts");

            migrationBuilder.DropTable(
                name: "enrichment_queue_entries");
        }
    }
}
