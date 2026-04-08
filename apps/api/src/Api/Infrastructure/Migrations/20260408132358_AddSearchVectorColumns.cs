using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchVectorColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-check: drop legacy 'text'-typed search_vector columns from old init SQL
            // (api-migrations-20251118.sql created search_vector as 'text' which is the wrong type
            // for the @@ tsquery operator). Safe no-op when columns don't exist or are already tsvector.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'text_chunks'
                          AND column_name = 'search_vector'
                          AND data_type = 'text'
                    ) THEN
                        ALTER TABLE text_chunks DROP COLUMN search_vector;
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'pdf_documents'
                          AND column_name = 'search_vector'
                          AND data_type = 'text'
                    ) THEN
                        ALTER TABLE pdf_documents DROP COLUMN search_vector;
                    END IF;
                END $$;
            ");

            // text_chunks.search_vector: GENERATED stored tsvector from Content column
            migrationBuilder.Sql(@"
                ALTER TABLE text_chunks
                ADD COLUMN IF NOT EXISTS search_vector tsvector
                GENERATED ALWAYS AS (to_tsvector('english', ""Content"")) STORED;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_text_chunks_search_vector
                ON text_chunks USING gin (search_vector);
            ");

            // pdf_documents.search_vector: GENERATED stored tsvector from ExtractedText + FileName
            migrationBuilder.Sql(@"
                ALTER TABLE pdf_documents
                ADD COLUMN IF NOT EXISTS search_vector tsvector
                GENERATED ALWAYS AS (
                    to_tsvector('english',
                        coalesce(""ExtractedText"", '') || ' ' || coalesce(""FileName"", ''))
                ) STORED;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_pdf_documents_search_vector
                ON pdf_documents USING gin (search_vector);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_text_chunks_search_vector;");
            migrationBuilder.Sql("ALTER TABLE text_chunks DROP COLUMN IF EXISTS search_vector;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_pdf_documents_search_vector;");
            migrationBuilder.Sql("ALTER TABLE pdf_documents DROP COLUMN IF EXISTS search_vector;");
        }
    }
}
