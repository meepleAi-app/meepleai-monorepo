using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGlossaryContexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contexts",
                schema: "session_tracking",
                table: "gamebook_glossary_entries",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            // #2638 / SI-7: backfill the multi-context list from the legacy single-context
            // pointer. Keys are PascalCase to match System.Text.Json default serialization
            // (the C# GlossaryContext record round-trips {BookId, ParagraphRef, Definition}).
            // Rows with no first_seen_book_id keep the "[]" default.
            migrationBuilder.Sql(
                "UPDATE session_tracking.gamebook_glossary_entries " +
                "SET contexts = jsonb_build_array(jsonb_build_object(" +
                "'BookId', first_seen_book_id::text, 'ParagraphRef', NULL, 'Definition', NULL)) " +
                "WHERE first_seen_book_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contexts",
                schema: "session_tracking",
                table: "gamebook_glossary_entries");
        }
    }
}
