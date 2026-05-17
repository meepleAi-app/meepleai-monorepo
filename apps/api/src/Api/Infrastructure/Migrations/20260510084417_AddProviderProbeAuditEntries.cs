using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderProbeAuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_probe_audit_entries",
                schema: "administration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_fingerprint = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    outcome = table.Column<int>(type: "integer", nullable: false),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    probed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_probe_audit_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_probe_audit_actor_probed_at",
                schema: "administration",
                table: "provider_probe_audit_entries",
                columns: new[] { "actor_id", "probed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_provider_probe_audit_provider_probed_at",
                schema: "administration",
                table: "provider_probe_audit_entries",
                columns: new[] { "provider_name", "probed_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_probe_audit_entries",
                schema: "administration");
        }
    }
}
