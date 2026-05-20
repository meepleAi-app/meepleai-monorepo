using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageOperationOutbox_Issue1314 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "storage_operation_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MigrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegacyKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    NewKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_operation_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_storage_operation_outbox_legacy_key",
                table: "storage_operation_outbox",
                column: "LegacyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_storage_operation_outbox_migration_id",
                table: "storage_operation_outbox",
                column: "MigrationId");

            migrationBuilder.CreateIndex(
                name: "IX_storage_operation_outbox_status_scheduled_at",
                table: "storage_operation_outbox",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storage_operation_outbox");
        }
    }
}
