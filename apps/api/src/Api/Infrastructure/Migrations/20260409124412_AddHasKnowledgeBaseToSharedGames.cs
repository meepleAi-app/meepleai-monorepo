using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHasKnowledgeBaseToSharedGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_knowledge_base",
                table: "shared_games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_shared_games_has_knowledge_base",
                table: "shared_games",
                column: "has_knowledge_base",
                filter: "has_knowledge_base = true");

            // S2 (library-to-game epic) — backfill: mark games with at least one
            // indexed vector document as having KB. Uses vector_documents.shared_game_id
            // cross-BC reference (Issue #4921). The "IndexingStatus" column is
            // PascalCase (default EF naming) hence the quoted identifier.
            migrationBuilder.Sql(@"
                UPDATE shared_games
                SET has_knowledge_base = true
                WHERE id IN (
                    SELECT DISTINCT shared_game_id
                    FROM vector_documents
                    WHERE shared_game_id IS NOT NULL
                      AND ""IndexingStatus"" = 'completed'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shared_games_has_knowledge_base",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "has_knowledge_base",
                table: "shared_games");
        }
    }
}
