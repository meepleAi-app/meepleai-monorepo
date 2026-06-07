using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyGuardsToAlertChannels_Iso2 : Migration
    {
        // PR comment: 2 modifiche di iso-2 (alert_channels.last_dispatched_event_id +
        // nuova tabella health_status_alerts_sent). Le 5 colonne test_run_id rilevate
        // come drift sono manualmente rimosse: sono già presenti nelle migration di
        // CF-2 (#1946), CF-3 (#1947) e iso-1 (#1949). Concurrent merge garantisce
        // single application.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "last_dispatched_event_id",
                table: "alert_channels",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "health_status_alerts_sent",
                columns: table => new
                {
                    service_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_status_alerts_sent", x => x.service_name);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "health_status_alerts_sent");

            migrationBuilder.DropColumn(
                name: "last_dispatched_event_id",
                table: "alert_channels");
        }
    }
}
