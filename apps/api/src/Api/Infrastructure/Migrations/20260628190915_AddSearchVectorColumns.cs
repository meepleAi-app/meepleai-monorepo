using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #2559 — restore the full-text <c>search_vector</c> columns that hybrid/keyword
    /// search depends on. <see cref="Api.Services.KeywordSearchService"/> queries
    /// <c>text_chunks.search_vector</c> and <c>pdf_documents.search_vector</c>, and both
    /// <c>TextChunkEntity</c> / <c>PdfDocumentEntity</c> map+<c>Ignore</c> a
    /// <c>SearchVector</c> property documented as "computed via migration
    /// AddSearchVectorColumns". That migration's DDL was lost in the migration squash
    /// (InitialCreate kept only the pgvector_embeddings search_vector), so every
    /// keyword/hybrid search failed with <c>42703: column "search_vector" does not
    /// exist</c> and cross-game ask returned 0 citations. Re-create the columns as
    /// STORED GENERATED tsvectors (same pattern as pgvector_embeddings) + GIN indexes.
    /// Idempotent (IF NOT EXISTS) so it is safe on any DB whose table predates this fix.
    /// </summary>
    public partial class AddSearchVectorColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE text_chunks
                    ADD COLUMN IF NOT EXISTS search_vector tsvector
                    GENERATED ALWAYS AS (to_tsvector('english', "Content")) STORED;
                """);
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_text_chunks_search_vector
                    ON text_chunks USING gin (search_vector);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE pdf_documents
                    ADD COLUMN IF NOT EXISTS search_vector tsvector
                    GENERATED ALWAYS AS (to_tsvector('english', COALESCE("ExtractedText", ''))) STORED;
                """);
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_pdf_documents_search_vector
                    ON pdf_documents USING gin (search_vector);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_pdf_documents_search_vector;");
            migrationBuilder.Sql("ALTER TABLE pdf_documents DROP COLUMN IF EXISTS search_vector;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_text_chunks_search_vector;");
            migrationBuilder.Sql("ALTER TABLE text_chunks DROP COLUMN IF EXISTS search_vector;");
        }
    }
}
