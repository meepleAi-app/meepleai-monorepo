using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #950 (W1-PR2) — enable sub-100ms p95 autocomplete on
    /// <c>GET /api/v1/users/search?q=</c> which reuses
    /// <see cref="Api.BoundedContexts.Administration.Application.Queries.SearchUsersQueryHandler"/>.
    /// Without these indexes the underlying ILIKE scan on <c>users.display_name</c>
    /// and <c>users.email</c> degrades from ~200ms at 1K users to &gt; 1s at 10K
    /// (Nygard panel P1 — see spec §15 in
    /// <c>docs/superpowers/specs/2026-05-18-sp7-game-night-create.md</c>).
    ///
    /// Forward-only and rollback-safe:
    /// — <c>CREATE EXTENSION</c> + <c>CREATE INDEX</c> are additive.
    /// — <c>IF NOT EXISTS</c> guards make UP idempotent for re-runs.
    /// — DOWN drops the indexes but keeps the extension (other features
    ///   may depend on pg_trgm; extension removal is an ops decision,
    ///   not a code rollback concern).
    /// </summary>
    public partial class AddPgTrgmIndexUsersDisplayNameEmail_Issue950 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE EXTENSION IF NOT EXISTS pg_trgm;
            ");

            // Column identifiers are PascalCase in this schema (see Initial migration:
            // table.Column<string>(... DisplayName / Email ...)). The quotes around the
            // identifiers preserve casing at execution time — bare gin(DisplayName ...)
            // would be folded to lowercase and resolve to a non-existent column.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_users_display_name_trgm
                  ON users USING gin (""DisplayName"" gin_trgm_ops);
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_users_email_trgm
                  ON users USING gin (""Email"" gin_trgm_ops);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_users_email_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_users_display_name_trgm;");
            // pg_trgm extension intentionally left in place: shared resource.
        }
    }
}
