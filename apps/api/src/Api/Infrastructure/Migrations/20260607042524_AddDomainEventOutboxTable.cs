using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainEventOutboxTable : Migration
    {
        // PR comment: 1 nuova tabella domain_event_outbox + 2 partial indexes per
        // issue #1535. Le colonne test_run_id rilevate come drift di asse-d task B
        // (#1928) sono manualmente rimosse: sono già presenti in altre migration
        // pending (CF-2 #1946, CF-3 #1947, iso-1 #1949, iso-2 #1950). Concurrent
        // merge garantisce single application.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "domain_event_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    payload_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<byte>(type: "smallint", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enqueued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_event_outbox", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_domain_event_outbox_failed_recent",
                table: "domain_event_outbox",
                column: "enqueued_at",
                descending: new bool[0],
                filter: "status = 2");

            migrationBuilder.CreateIndex(
                name: "ix_domain_event_outbox_pending",
                table: "domain_event_outbox",
                columns: new[] { "next_attempt_at", "enqueued_at" },
                filter: "status = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "domain_event_outbox");
        }
    }
}
