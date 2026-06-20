using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayRecordXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin is a PostgreSQL system column (transaction ID of the last update).
            // It exists on every Postgres table by default and does NOT need to be created.
            // This migration carries only the EF model snapshot update so that EF can round-trip
            // the value as a concurrency token (ADR-060). #2436 PR-B / #2437-1.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // xmin is a PostgreSQL system column — nothing to drop.
        }
    }
}
