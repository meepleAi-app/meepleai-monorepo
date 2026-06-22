using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcknowledgeAndTriggerSourceToWikidataAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "acknowledged_at",
                table: "wikidata_cover_enrichment_attempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "acknowledged_by",
                table: "wikidata_cover_enrichment_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "triggered_by_admin_user_id",
                table: "wikidata_cover_enrichment_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_wikidata_cover_attempts_acknowledged_at",
                table: "wikidata_cover_enrichment_attempts",
                column: "acknowledged_at",
                filter: "acknowledged_at IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wikidata_cover_attempts_acknowledged_at",
                table: "wikidata_cover_enrichment_attempts");

            migrationBuilder.DropColumn(
                name: "acknowledged_at",
                table: "wikidata_cover_enrichment_attempts");

            migrationBuilder.DropColumn(
                name: "acknowledged_by",
                table: "wikidata_cover_enrichment_attempts");

            migrationBuilder.DropColumn(
                name: "triggered_by_admin_user_id",
                table: "wikidata_cover_enrichment_attempts");
        }
    }
}
