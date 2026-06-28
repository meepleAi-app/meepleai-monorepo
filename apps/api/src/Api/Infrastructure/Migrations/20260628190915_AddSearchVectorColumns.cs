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
    /// STORED GENERATED tsvectors + GIN indexes.
    /// Idempotent (IF NOT EXISTS) so it is safe on any DB whose table predates this fix.
    /// <para>
    /// FTS config is <c>'italian'</c> (NOT <c>'english'</c>): the runtime query path
    /// (<see cref="Api.Services.HybridSearchService"/> → <c>KeywordSearchService.SearchAsync</c>)
    /// never passes a language and therefore defaults to <c>language = "it"</c> →
    /// <c>to_tsquery('italian'::regconfig, …)</c>. The stored tsvector config MUST match
    /// the tsquery config or the <c>@@</c> operator silently returns nothing — e.g. an
    /// 'english' vector drops "how/do/up" as English stopwords at index time, but the
    /// Italian query still requires them, so the AND-query never matches. Using 'italian'
    /// on both sides applies the same stemmer symmetrically, so it matches regardless of
    /// the document's actual language. (pgvector_embeddings.search_vector stays 'english'
    /// because its query path, PgVectorStoreAdapter, uses 'english' — a separate column.)
    /// </para>
    /// </summary>
    public partial class AddSearchVectorColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE text_chunks
                    ADD COLUMN IF NOT EXISTS search_vector tsvector
                    GENERATED ALWAYS AS (to_tsvector('italian', "Content")) STORED;
                """);
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_text_chunks_search_vector
                    ON text_chunks USING gin (search_vector);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE pdf_documents
                    ADD COLUMN IF NOT EXISTS search_vector tsvector
                    GENERATED ALWAYS AS (to_tsvector('italian', COALESCE("ExtractedText", ''))) STORED;
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
