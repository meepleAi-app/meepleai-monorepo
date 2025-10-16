using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextAndVectorSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===================================================================
            // DB-03: Full-Text Search Indexes for PDF and RuleSpec
            // ===================================================================
            // Requirement: Query searches on demo dataset < 200ms
            // These GIN indexes enable fast full-text search on PostgreSQL

            // 1. Full-text search index on pdf_documents.ExtractedText
            // Enables: SELECT * FROM pdf_documents WHERE to_tsvector('english', "ExtractedText") @@ plainto_tsquery('search term')
            // Use case: User searches for keywords in uploaded PDF rulebooks
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_pdf_documents_ExtractedText_GIN""
                ON pdf_documents USING GIN (to_tsvector('english', COALESCE(""ExtractedText"", '')));
            ");

            // 2. Full-text search index on rule_atoms.Text
            // Enables: SELECT * FROM rule_atoms WHERE to_tsvector('english', ""Text"") @@ plainto_tsquery('search term')
            // Use case: Search within structured rule components (setup, actions, victory conditions)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_rule_atoms_Text_GIN""
                ON rule_atoms USING GIN (to_tsvector('english', ""Text""));
            ");

            // ===================================================================
            // DB-03: Composite Indexes for Filtered Queries
            // ===================================================================
            // Optimizes common query patterns with multiple WHERE conditions

            // 3. Composite index on pdf_documents(GameId, ProcessingStatus) for filtered listing
            // Enables: SELECT * FROM pdf_documents WHERE "GameId" = ? AND "ProcessingStatus" = 'completed'
            // Use case: List all completed PDFs for a specific game (very common in RAG pipeline)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_pdf_documents_GameId_ProcessingStatus""
                ON pdf_documents (""GameId"", ""ProcessingStatus"");
            ");

            // 4. Composite index on pdf_documents(GameId, UploadedAt DESC) for temporal queries
            // Enables: SELECT * FROM pdf_documents WHERE "GameId" = ? ORDER BY "UploadedAt" DESC
            // Use case: Show most recent PDFs for a game (admin dashboard, upload history)
            // Note: Complements existing IX_pdf_documents_UploadedByUserId_UploadedAt_Desc
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_pdf_documents_GameId_UploadedAt_Desc""
                ON pdf_documents (""GameId"", ""UploadedAt"" DESC);
            ");

            // 5. Composite index on rule_atoms(RuleSpecId, Text) for spec-scoped search
            // Enables: SELECT * FROM rule_atoms WHERE "RuleSpecId" = ? AND "Text" LIKE '%keyword%'
            // Use case: Search within a specific rule specification version
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_rule_atoms_RuleSpecId_Text""
                ON rule_atoms (""RuleSpecId"", ""Text"");
            ");

            // NOTE: Vector search indexes for Qdrant are managed separately in the Qdrant service
            // Qdrant automatically creates HNSW indexes on vector fields when collections are initialized
            // See: QdrantService.EnsureCollectionExistsAsync() for vector index configuration
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes in reverse order
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_rule_atoms_RuleSpecId_Text"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_pdf_documents_GameId_UploadedAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_pdf_documents_GameId_ProcessingStatus"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_rule_atoms_Text_GIN"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_pdf_documents_ExtractedText_GIN"";");
        }
    }
}
