using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleActiveDocumentConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========================================================================
            // ISSUE #2410: Add concurrency protection for version activation
            // Problem: Race condition - concurrent SetActiveVersionAsync calls could
            //          create multiple active versions for same (shared_game_id, document_type)
            // Solution: Unique partial index ensures only ONE active document per game+type
            // ========================================================================

            // Unique partial index: Guarantees at most one active document per (game, type)
            // PostgreSQL partial indexes filter rows with WHERE clause before applying uniqueness
            // This is more efficient than a full unique constraint with nullable column pattern
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ix_shared_game_documents_single_active
                ON shared_game_documents (shared_game_id, document_type)
                WHERE is_active = true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_game_documents_single_active;");
        }
    }
}
