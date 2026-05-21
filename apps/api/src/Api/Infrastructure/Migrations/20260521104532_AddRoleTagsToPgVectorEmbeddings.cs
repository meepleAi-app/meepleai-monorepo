using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleTagsToPgVectorEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #1391: denormalize text_chunks.role_tags onto pgvector_embeddings so
            // semantic-mode hybrid search can apply the role-match boost without joining.
            // pgvector_embeddings is NOT EF-managed (created via raw SQL by
            // PgVectorStoreAdapter.EnsureCollectionExistsAsync). The ADD COLUMN here mirrors
            // the DDL change in that adapter — both code paths must stay in sync.
            // Idempotent: IF NOT EXISTS keeps the migration safe to re-run.
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS pgvector_embeddings
                ADD COLUMN IF NOT EXISTS role_tags INTEGER NOT NULL DEFAULT 0;
                """);

            // Backfill existing rows from text_chunks. Uses source_chunk_id as the join key
            // (already present on pgvector_embeddings). Only updates rows that still hold the
            // default (0) to keep this idempotent.
            // text_chunks PK column is PascalCase quoted "Id" (EF default — no [Column] override),
            // while role_tags is snake_case (explicit HasColumnName mapping on TextChunkEntity).
            migrationBuilder.Sql("""
                UPDATE pgvector_embeddings pve
                SET role_tags = tc.role_tags
                FROM text_chunks tc
                WHERE pve.source_chunk_id = tc."Id"
                  AND pve.role_tags = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // WARNING: dropping the column destroys the denormalized role_tags values.
            // Re-running Up() restores them by re-backfilling from text_chunks
            // (the source of truth on which the sync invariant is anchored), but any
            // ingestion that ran while Up was reverted will have written zeroes.
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS pgvector_embeddings
                DROP COLUMN IF EXISTS role_tags;
                """);
        }
    }
}
