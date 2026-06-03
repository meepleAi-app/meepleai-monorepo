using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_CatalogSyncRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_sync_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    triggered_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    items_added = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    items_updated = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    items_failed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_detail = table.Column<string>(type: "text", nullable: true),
                    log_tail_json_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_sync_runs", x => x.id);
                    table.CheckConstraint("ck_catalog_sync_runs_provider_range", "provider BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_catalog_sync_runs_status_range", "status BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_catalog_sync_runs_users_triggered_by_user_id",
                        column: x => x.triggered_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_catalog_sync_runs_started_at",
                table: "catalog_sync_runs",
                column: "started_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_sync_runs_status",
                table: "catalog_sync_runs",
                column: "status",
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_sync_runs_triggered_by_user_id",
                table: "catalog_sync_runs",
                column: "triggered_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_sync_runs");
        }
    }
}
