using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogSeedDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_seed_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bgg_id = table.Column<int>(type: "integer", nullable: true),
                    wikidata_qid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    search_term_input = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provenance_json = table.Column<string>(type: "jsonb", nullable: true),
                    raw_payload_json = table.Column<string>(type: "jsonb", nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resulting_shared_game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_seed_drafts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_catalog_seed_drafts_bgg_id",
                table: "catalog_seed_drafts",
                column: "bgg_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_seed_drafts_created_at",
                table: "catalog_seed_drafts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_seed_drafts_status",
                table: "catalog_seed_drafts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_seed_drafts_wikidata_qid",
                table: "catalog_seed_drafts",
                column: "wikidata_qid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_seed_drafts");
        }
    }
}
