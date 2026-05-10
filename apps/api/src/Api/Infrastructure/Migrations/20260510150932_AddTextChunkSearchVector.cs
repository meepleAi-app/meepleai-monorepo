using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTextChunkSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #927: text_chunks.search_vector column was referenced in
            // SearchKbChunksHandler raw SQL + TextChunkEntityConfiguration comment
            // but never materialised by any prior migration, causing 42703
            // ("column does not exist") on every QA stream request that hit
            // the keyword/hybrid search path.
            //
            // GENERATED ALWAYS AS ... STORED keeps the column in sync with
            // Content automatically (DB-managed, EF Core ignores writes).
            // Language must match plainto_tsquery('simple', ...) used by
            // SearchKbChunksHandler line 91 to keep ranking consistent.
            migrationBuilder.Sql(@"
                ALTER TABLE text_chunks
                  ADD COLUMN IF NOT EXISTS search_vector tsvector
                    GENERATED ALWAYS AS (to_tsvector('simple', ""Content"")) STORED;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_text_chunks_search_vector
                  ON text_chunks USING gin (search_vector);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_text_chunks_search_vector;");
            migrationBuilder.Sql("ALTER TABLE text_chunks DROP COLUMN IF EXISTS search_vector;");
        }
    }
}
