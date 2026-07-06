using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RowVersionToXminSweep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Switch AlertChannel / AppBudget / GameToolkit / ToolkitVersion optimistic
            // concurrency from the broken bytea RowVersion pattern (EF omits the store-generated
            // column on INSERT → NULL → 23502) to Postgres's `xmin` system column, mirroring
            // 20260614113542_LiveSessionRowVersionToXmin. `xmin` is managed by Postgres and must
            // NOT be created as a real column — EF maps it via .HasColumnType("xid"). The
            // AddColumn("xmin", ...) operations EF scaffolds here are intentionally removed;
            // only the legacy bytea columns are dropped.
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ToolkitVersions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "app_budgets");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "alert_channels");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the legacy bytea concurrency columns (the reverse of Up). No xmin drop:
            // xmin is a system column that was never created by Up.
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ToolkitVersions",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "GameToolkits",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "app_budgets",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "alert_channels",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
