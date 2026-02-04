using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBggImportQueueTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BggImportQueue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BggId = table.Column<int>(type: "integer", nullable: false),
                    GameName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BggImportQueue", x => x.Id);
                });

            // Index for finding next queued item to process (ordered by Position)
            migrationBuilder.CreateIndex(
                name: "IX_BggImportQueue_Status_Position",
                table: "BggImportQueue",
                columns: new[] { "Status", "Position" },
                filter: "\"Status\" = 0"); // BggImportStatus.Queued = 0

            // Index for duplicate BggId detection
            migrationBuilder.CreateIndex(
                name: "IX_BggImportQueue_BggId",
                table: "BggImportQueue",
                column: "BggId");

            // Index for cleanup queries (find old completed/failed jobs)
            migrationBuilder.CreateIndex(
                name: "IX_BggImportQueue_Status_ProcessedAt",
                table: "BggImportQueue",
                columns: new[] { "Status", "ProcessedAt" },
                filter: "\"Status\" IN (2, 3)"); // BggImportStatus.Completed = 2, Failed = 3

            // Index for created game tracking
            migrationBuilder.CreateIndex(
                name: "IX_BggImportQueue_CreatedGameId",
                table: "BggImportQueue",
                column: "CreatedGameId",
                filter: "\"CreatedGameId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BggImportQueue_Status_Position",
                table: "BggImportQueue");

            migrationBuilder.DropIndex(
                name: "IX_BggImportQueue_BggId",
                table: "BggImportQueue");

            migrationBuilder.DropIndex(
                name: "IX_BggImportQueue_Status_ProcessedAt",
                table: "BggImportQueue");

            migrationBuilder.DropIndex(
                name: "IX_BggImportQueue_CreatedGameId",
                table: "BggImportQueue");

            migrationBuilder.DropTable(
                name: "BggImportQueue");
        }
    }
}
