using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWikidataCoverEnrichmentAttemptsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wikidata_cover_enrichment_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    details = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dead_lettered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wikidata_cover_enrichment_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_wikidata_cover_enrichment_attempts_shared_games_shared_game~",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_wikidata_cover_attempts_dead_letter",
                table: "wikidata_cover_enrichment_attempts",
                column: "dead_lettered_at",
                filter: "dead_lettered_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_wikidata_cover_attempts_game_latest",
                table: "wikidata_cover_enrichment_attempts",
                columns: new[] { "shared_game_id", "attempted_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_wikidata_cover_attempts_next_retry",
                table: "wikidata_cover_enrichment_attempts",
                column: "next_retry_at",
                filter: "next_retry_at IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wikidata_cover_enrichment_attempts");
        }
    }
}
