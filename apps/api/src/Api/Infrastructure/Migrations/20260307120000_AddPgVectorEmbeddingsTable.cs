using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPgVectorEmbeddingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable pgvector extension
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector");

            // Create pgvector_embeddings table (not EF-managed, used by PgVectorStoreAdapter)
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS pgvector_embeddings (
                    id UUID PRIMARY KEY,
                    vector_document_id UUID NOT NULL,
                    game_id UUID NOT NULL,
                    text_content TEXT NOT NULL,
                    vector vector(1024) NOT NULL,
                    model TEXT NOT NULL,
                    chunk_index INTEGER NOT NULL,
                    page_number INTEGER NOT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                )
                """);

            // HNSW index for cosine similarity search (m=16, ef_construction=200 per design doc)
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_pgvector_embeddings_vector_cosine
                ON pgvector_embeddings
                USING hnsw (vector vector_cosine_ops)
                WITH (m = 16, ef_construction = 200)
                """);

            // B-tree index on game_id for WHERE filtering
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_pgvector_embeddings_game_id
                ON pgvector_embeddings (game_id)
                """);

            // B-tree index on vector_document_id for deletion
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_pgvector_embeddings_vector_document_id
                ON pgvector_embeddings (vector_document_id)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS pgvector_embeddings");
        }
    }
}
