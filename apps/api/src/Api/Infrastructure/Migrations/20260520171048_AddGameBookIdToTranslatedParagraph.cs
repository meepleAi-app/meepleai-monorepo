using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameBookIdToTranslatedParagraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_translated_paragraphs_campaign_paragraph",
                schema: "session_tracking",
                table: "translated_paragraphs");

            // C3 (2026-05-19): GameBookId NOT NULL. Default = Guid.Empty is acceptable here
            // because the table is empty in dev/staging (this is in-progress Phase C work,
            // pre-Phase-E client wiring — no production data exists). If any row sneaks in,
            // the new composite UNIQUE index (campaign_id, game_book_id, paragraph_number)
            // would catch collisions on the empty-Guid sentinel — surfaced loudly, not silently.
            migrationBuilder.AddColumn<Guid>(
                name: "game_book_id",
                schema: "session_tracking",
                table: "translated_paragraphs",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.CreateIndex(
                name: "ux_translated_paragraphs_campaign_book_paragraph",
                schema: "session_tracking",
                table: "translated_paragraphs",
                columns: new[] { "campaign_id", "game_book_id", "paragraph_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_translated_paragraphs_campaign_book_paragraph",
                schema: "session_tracking",
                table: "translated_paragraphs");

            migrationBuilder.DropColumn(
                name: "game_book_id",
                schema: "session_tracking",
                table: "translated_paragraphs");

            migrationBuilder.CreateIndex(
                name: "ix_translated_paragraphs_campaign_paragraph",
                schema: "session_tracking",
                table: "translated_paragraphs",
                columns: new[] { "campaign_id", "paragraph_number" });
        }
    }
}
