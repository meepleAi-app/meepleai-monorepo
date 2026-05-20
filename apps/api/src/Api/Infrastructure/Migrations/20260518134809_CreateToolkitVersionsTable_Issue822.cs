using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #822 — Phase 5 schema foundation: create <c>ToolkitVersions</c>
    /// table for marketplace version history and backfill one row per published
    /// <c>GameToolkit</c> from its current <c>VersionSemver</c> string.
    ///
    /// Forward-only and rollback-safe per rollback-runbook section 8:
    /// — CREATE TABLE is additive.
    /// — Backfill INSERT is idempotent via <c>ON CONFLICT (ToolkitId, VersionNumber) DO NOTHING</c>.
    /// — DOWN drops the table (no production data loss because reads do not
    ///   depend on it until PR-2 ships the endpoints; the legacy
    ///   <c>GetToolkitVersionsQueryHandler</c> falls back to the stub if the
    ///   table is missing).
    /// </summary>
    public partial class CreateToolkitVersionsTable_Issue822 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToolkitVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolkitId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Changelog = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    YankedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    YankReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    YankedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolkitVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolkitVersions_GameToolkits_ToolkitId",
                        column: x => x.ToolkitId,
                        principalTable: "GameToolkits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToolkitVersions_ToolkitId_PublishedAt",
                table: "ToolkitVersions",
                columns: new[] { "ToolkitId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolkitVersions_ToolkitId_VersionNumber",
                table: "ToolkitVersions",
                columns: new[] { "ToolkitId", "VersionNumber" },
                unique: true);

            // Backfill one row per published GameToolkit using its current
            // VersionSemver (already backfilled by PR #1144 to "0.{int}.0"
            // shape from the legacy int Version field). Idempotent via
            // ON CONFLICT — re-running the migration is a no-op.
            //
            // Draft toolkits (IsPublished=false) are NOT backfilled: they have
            // never reached a published-version state and the GetToolkitVersions
            // query will continue to return an empty list for them until the
            // owner publishes a real version via PR-2 endpoints.
            //
            // RowVersion is initialised to an empty bytea; Postgres + EF Core
            // will increment it on the first UPDATE (the yank path in PR-2).
            migrationBuilder.Sql(@"
                INSERT INTO ""ToolkitVersions"" (
                    ""Id"",
                    ""ToolkitId"",
                    ""VersionNumber"",
                    ""Changelog"",
                    ""PublishedAt"",
                    ""PublishedBy"",
                    ""YankedAt"",
                    ""YankReason"",
                    ""YankedBy"",
                    ""RowVersion""
                )
                SELECT
                    gen_random_uuid(),
                    gt.""Id"",
                    gt.""VersionSemver"",
                    NULL,
                    COALESCE(gt.""UpdatedAt"", gt.""CreatedAt""),
                    gt.""CreatedByUserId"",
                    NULL,
                    NULL,
                    NULL,
                    E'\\x00000000'::bytea
                FROM ""GameToolkits"" gt
                WHERE gt.""IsPublished"" = TRUE
                ON CONFLICT (""ToolkitId"", ""VersionNumber"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolkitVersions");
        }
    }
}
