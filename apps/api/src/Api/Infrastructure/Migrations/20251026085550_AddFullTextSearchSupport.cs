using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearchSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create text_chunks table for keyword search (mirrors Qdrant data)
            migrationBuilder.Sql(@"
                CREATE TABLE text_chunks (
                    id TEXT PRIMARY KEY,
                    game_id TEXT NOT NULL,
                    pdf_document_id TEXT NOT NULL,
                    content TEXT NOT NULL,
                    chunk_index INTEGER NOT NULL,
                    page_number INTEGER,
                    character_count INTEGER NOT NULL,
                    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT fk_text_chunks_game FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE,
                    CONSTRAINT fk_text_chunks_pdf_document FOREIGN KEY (pdf_document_id) REFERENCES pdf_documents(id) ON DELETE CASCADE
                );

                CREATE INDEX idx_text_chunks_game_id ON text_chunks(game_id);
                CREATE INDEX idx_text_chunks_pdf_document_id ON text_chunks(pdf_document_id);
            ");

            // 2. Add search_vector tsvector column to pdf_documents
            migrationBuilder.Sql(@"
                ALTER TABLE pdf_documents
                ADD COLUMN search_vector tsvector;
            ");

            // 3. Add search_vector tsvector column to text_chunks
            migrationBuilder.Sql(@"
                ALTER TABLE text_chunks
                ADD COLUMN search_vector tsvector;
            ");

            // 4. Create GIN indexes for full-text search (much faster than GiST for static data)
            migrationBuilder.Sql(@"
                CREATE INDEX idx_pdf_documents_search_vector
                ON pdf_documents USING GIN(search_vector);
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX idx_text_chunks_search_vector
                ON text_chunks USING GIN(search_vector);
            ");

            // 5. Populate existing pdf_documents search_vector from extracted_text
            // Uses English text search configuration with weights: A (title), B (content)
            migrationBuilder.Sql(@"
                UPDATE pdf_documents
                SET search_vector = to_tsvector('english',
                    COALESCE(file_name, '') || ' ' || COALESCE(extracted_text, '')
                )
                WHERE extracted_text IS NOT NULL;
            ");

            // 6. Create trigger function for automatic tsvector updates on pdf_documents
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION pdf_documents_search_vector_update()
                RETURNS trigger AS $$
                BEGIN
                    NEW.search_vector := to_tsvector('english',
                        COALESCE(NEW.file_name, '') || ' ' || COALESCE(NEW.extracted_text, '')
                    );
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER tsvector_update_pdf_documents
                BEFORE INSERT OR UPDATE ON pdf_documents
                FOR EACH ROW
                EXECUTE FUNCTION pdf_documents_search_vector_update();
            ");

            // 7. Create trigger function for automatic tsvector updates on text_chunks
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION text_chunks_search_vector_update()
                RETURNS trigger AS $$
                BEGIN
                    NEW.search_vector := to_tsvector('english', COALESCE(NEW.content, ''));
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER tsvector_update_text_chunks
                BEFORE INSERT OR UPDATE ON text_chunks
                FOR EACH ROW
                EXECUTE FUNCTION text_chunks_search_vector_update();
            ");

            // 8. Create additional indexes for common query patterns
            migrationBuilder.Sql(@"
                CREATE INDEX idx_text_chunks_chunk_index ON text_chunks(chunk_index);
                CREATE INDEX idx_text_chunks_page_number ON text_chunks(page_number) WHERE page_number IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove in reverse order

            // Drop additional indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_text_chunks_page_number;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_text_chunks_chunk_index;");

            // Drop text_chunks triggers and function
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tsvector_update_text_chunks ON text_chunks;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS text_chunks_search_vector_update();");

            // Drop pdf_documents triggers and function
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tsvector_update_pdf_documents ON pdf_documents;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS pdf_documents_search_vector_update();");

            // Drop GIN indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_text_chunks_search_vector;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_pdf_documents_search_vector;");

            // Drop search_vector columns
            migrationBuilder.Sql("ALTER TABLE text_chunks DROP COLUMN IF EXISTS search_vector;");
            migrationBuilder.Sql("ALTER TABLE pdf_documents DROP COLUMN IF EXISTS search_vector;");

            // Drop text_chunks table (CASCADE will drop foreign keys)
            migrationBuilder.Sql("DROP TABLE IF EXISTS text_chunks CASCADE;");
        }
    }
}
