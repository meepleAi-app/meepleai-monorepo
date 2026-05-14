using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #1144 — Stage 3 marketplace extension for GameToolkits.
    /// Spec: docs/superpowers/specs/2026-05-14-stage3-toolkit-detail.md §5.1
    ///
    /// VersionSemver migrates via the 3-step idempotent pattern documented in
    /// the spec so re-running the migration on a partially-backfilled DB does
    /// NOT overwrite legitimate user-assigned "0.1.0" values:
    ///   Step 1 — add column NULL.
    ///   Step 2 — backfill ONLY rows where the column is still NULL.
    ///   Step 3 — promote to NOT NULL with the default for future inserts.
    /// </summary>
    public partial class ExtendGameToolkitWithMarketplaceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Description + License — straight-add nullable columns ─────────
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "GameToolkits",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "License",
                table: "GameToolkits",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // ── VersionSemver — 3-step idempotent migration (spec §5.1) ──────
            // Step 1: add NULLABLE first so backfill can target unset rows only.
            migrationBuilder.AddColumn<string>(
                name: "VersionSemver",
                table: "GameToolkits",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Step 2: backfill ONLY rows where the column is still NULL.
            // Re-running the migration cannot overwrite a legitimate user value
            // (e.g. an admin sets "2.5.0" between Up() invocations during a
            // staged rollout) because those rows are no longer NULL.
            // COALESCE handles the edge case where legacy Version is NULL.
            migrationBuilder.Sql(@"
                UPDATE ""GameToolkits""
                SET ""VersionSemver"" = '0.' || COALESCE(""Version"", 0)::text || '.0'
                WHERE ""VersionSemver"" IS NULL;
            ");

            // Step 3: promote to NOT NULL with the default for future inserts.
            migrationBuilder.AlterColumn<string>(
                name: "VersionSemver",
                table: "GameToolkits",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "0.1.0",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "License",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "VersionSemver",
                table: "GameToolkits");
        }
    }
}
