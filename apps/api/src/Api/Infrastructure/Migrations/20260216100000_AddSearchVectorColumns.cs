using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores - EF Core migration naming convention
    public partial class AddSearchVectorColumns : Migration
#pragma warning restore CA1707
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add search_vector tsvector columns to text_chunks and pdf_documents.
            // These columns are managed by PostgreSQL triggers (not EF Core).
            // EF Core entity configurations use builder.Ignore(e => e.SearchVector).

            // 1. Add tsvector column to text_chunks (if not exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'text_chunks' AND column_name = 'search_vector'
                    ) THEN
                        ALTER TABLE text_chunks ADD COLUMN search_vector tsvector;
                    ELSE
                        -- Column exists but may be wrong type (text instead of tsvector)
                        ALTER TABLE text_chunks ALTER COLUMN search_vector TYPE tsvector
                            USING search_vector::tsvector;
                    END IF;
                END $$;
            ");

            // 2. Add tsvector column to pdf_documents (if not exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'pdf_documents' AND column_name = 'search_vector'
                    ) THEN
                        ALTER TABLE pdf_documents ADD COLUMN search_vector tsvector;
                    ELSE
                        ALTER TABLE pdf_documents ALTER COLUMN search_vector TYPE tsvector
                            USING search_vector::tsvector;
                    END IF;
                END $$;
            ");

            // 3. Create GIN indexes for full-text search performance
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_text_chunks_search_vector""
                    ON text_chunks USING GIN (search_vector);
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_pdf_documents_search_vector""
                    ON pdf_documents USING GIN (search_vector);
            ");

            // 4. Create trigger function to auto-populate text_chunks.search_vector from Content
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION tsvector_update_text_chunks()
                RETURNS trigger AS $$
                BEGIN
                    NEW.search_vector :=
                        setweight(to_tsvector('italian', COALESCE(NEW.""Content"", '')), 'A') ||
                        setweight(to_tsvector('english', COALESCE(NEW.""Content"", '')), 'B');
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_text_chunks_search_vector ON text_chunks;
                CREATE TRIGGER trg_text_chunks_search_vector
                    BEFORE INSERT OR UPDATE OF ""Content""
                    ON text_chunks
                    FOR EACH ROW
                    EXECUTE FUNCTION tsvector_update_text_chunks();
            ");

            // 5. Create trigger function to auto-populate pdf_documents.search_vector from FileName
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION tsvector_update_pdf_documents()
                RETURNS trigger AS $$
                BEGIN
                    NEW.search_vector :=
                        setweight(to_tsvector('italian', COALESCE(NEW.""FileName"", '')), 'A') ||
                        setweight(to_tsvector('english', COALESCE(NEW.""FileName"", '')), 'B');
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_pdf_documents_search_vector ON pdf_documents;
                CREATE TRIGGER trg_pdf_documents_search_vector
                    BEFORE INSERT OR UPDATE OF ""FileName""
                    ON pdf_documents
                    FOR EACH ROW
                    EXECUTE FUNCTION tsvector_update_pdf_documents();
            ");

            // 6. Populate search_vector for existing rows
            migrationBuilder.Sql(@"
                UPDATE text_chunks
                SET search_vector =
                    setweight(to_tsvector('italian', COALESCE(""Content"", '')), 'A') ||
                    setweight(to_tsvector('english', COALESCE(""Content"", '')), 'B')
                WHERE search_vector IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE pdf_documents
                SET search_vector =
                    setweight(to_tsvector('italian', COALESCE(""FileName"", '')), 'A') ||
                    setweight(to_tsvector('english', COALESCE(""FileName"", '')), 'B')
                WHERE search_vector IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_text_chunks_search_vector ON text_chunks;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS tsvector_update_text_chunks();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_pdf_documents_search_vector ON pdf_documents;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS tsvector_update_pdf_documents();");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_text_chunks_search_vector\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_pdf_documents_search_vector\";");
            migrationBuilder.Sql("ALTER TABLE text_chunks DROP COLUMN IF EXISTS search_vector;");
            migrationBuilder.Sql("ALTER TABLE pdf_documents DROP COLUMN IF EXISTS search_vector;");
        }
    }
}
