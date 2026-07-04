using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #2569 — switch the <c>text_chunks</c> / <c>pdf_documents</c> <c>search_vector</c>
    /// generated columns from the <c>'italian'</c> FTS config (set by AddSearchVectorColumns,
    /// #2559) to <c>'english'</c>, matching the content (<c>PdfDocument.Language</c> defaults to
    /// "en"), the pgvector path (<c>PgVectorStoreAdapter</c> uses 'english'), and the new
    /// <c>KeywordSearchService</c> default (also 'english'). With an 'italian' column but an
    /// 'english' query the <c>@@</c> operator silently mismatched English content stemming.
    /// Postgres 16 cannot ALTER a generated column's expression, so the column is dropped and
    /// re-added; <c>search_vector</c> is GENERATED from already-present source columns, so it
    /// auto-repopulates. Idempotent (IF EXISTS / IF NOT EXISTS).
    /// </summary>
    public partial class AlignSearchVectorToEnglishFtsConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS idx_text_chunks_search_vector;
                ALTER TABLE text_chunks DROP COLUMN IF EXISTS search_vector;
                ALTER TABLE text_chunks
                    ADD COLUMN IF NOT EXISTS search_vector tsvector
                    GENERATED ALWAYS AS (to_tsvector('english', "Content")) STORED;
                CREATE INDEX IF NOT EXISTS idx_text_chunks_search_vector
                    ON text_chunks USING gin (search_vector);
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS idx_pdf_documents_search_vector;
                ALTER TABLE pdf_documents DROP COLUMN IF EXISTS search_vector;
                ALTER TABLE pdf_documents
                    ADD COLUMN IF NOT EXISTS search_vector tsvector
                    GENERATED ALWAYS AS (to_tsvector('english', COALESCE("ExtractedText", ''))) STORED;
                CREATE INDEX IF NOT EXISTS idx_pdf_documents_search_vector
                    ON pdf_documents USING gin (search_vector);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to the #2559 'italian' config.
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS idx_pdf_documents_search_vector;
                ALTER TABLE pdf_documents DROP COLUMN IF EXISTS search_vector;
                ALTER TABLE pdf_documents
                    ADD COLUMN IF NOT EXISTS search_vector tsvector
                    GENERATED ALWAYS AS (to_tsvector('italian', COALESCE("ExtractedText", ''))) STORED;
                CREATE INDEX IF NOT EXISTS idx_pdf_documents_search_vector
                    ON pdf_documents USING gin (search_vector);
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS idx_text_chunks_search_vector;
                ALTER TABLE text_chunks DROP COLUMN IF EXISTS search_vector;
                ALTER TABLE text_chunks
                    ADD COLUMN IF NOT EXISTS search_vector tsvector
                    GENERATED ALWAYS AS (to_tsvector('italian', "Content")) STORED;
                CREATE INDEX IF NOT EXISTS idx_text_chunks_search_vector
                    ON text_chunks USING gin (search_vector);
                """);
        }
    }
}
