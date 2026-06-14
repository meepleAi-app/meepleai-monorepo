using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MechanicDraftRowVersionToXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the silently-unused bytea column (Issue #2306).
            // The row_version column on mechanic_drafts was created via IsRowVersion()
            // but no trigger was ever created to populate it — EF's concurrency check always
            // compared an empty byte[] against itself, making all concurrent updates succeed.
            // xmin is a system column managed by Postgres; no DDL is needed to add it.
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "mechanic_drafts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the legacy bytea column if rolling back.
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "mechanic_drafts",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
