using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Adds GIN index on tags_json column for optimized JSONB array contains queries.
    /// Related: Issue #2409 - Optimize tag search with JSONB operators.
    /// </summary>
    public partial class AddGinIndexForTagsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create GIN index for JSONB key existence queries using ?| operator
            // Uses default jsonb_ops (not jsonb_path_ops) to support ?|, ?&, ? operators
            // This enables O(log n) lookups instead of O(n) full table scans
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS ix_shared_game_documents_tags_json_gin
                ON shared_game_documents
                USING gin (tags_json);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS ix_shared_game_documents_tags_json_gin;
                """);
        }
    }
}
