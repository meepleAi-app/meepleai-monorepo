using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionSnapshotTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_index = table.Column<int>(type: "integer", nullable: false),
                    trigger_type = table.Column<int>(type: "integer", nullable: false),
                    trigger_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    delta_data_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_checkpoint = table.Column<bool>(type: "boolean", nullable: false),
                    turn_index = table.Column<int>(type: "integer", nullable: false),
                    phase_index = table.Column<int>(type: "integer", nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_player_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_session_snapshots_session_id",
                table: "session_snapshots",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_snapshots_session_id_is_checkpoint",
                table: "session_snapshots",
                columns: new[] { "session_id", "is_checkpoint" });

            migrationBuilder.CreateIndex(
                name: "ix_session_snapshots_session_id_snapshot_index",
                table: "session_snapshots",
                columns: new[] { "session_id", "snapshot_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_snapshots_timestamp",
                table: "session_snapshots",
                column: "timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_snapshots");
        }
    }
}
