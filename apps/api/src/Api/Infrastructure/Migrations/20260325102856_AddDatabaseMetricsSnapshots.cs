using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseMetricsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "database_metrics_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    table_count = table.Column<int>(type: "integer", nullable: false),
                    index_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    active_connections = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_metrics_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_database_metrics_snapshots_recorded_at",
                table: "database_metrics_snapshots",
                column: "recorded_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "database_metrics_snapshots");
        }
    }
}
