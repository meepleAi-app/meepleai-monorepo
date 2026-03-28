using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for PgVectorEmbeddingEntity.
/// Maps to the pgvector_embeddings table created by PgVectorStoreAdapter raw SQL.
/// Used for typed queries during RAG data export/import operations.
/// </summary>
internal class PgVectorEmbeddingEntityConfiguration : IEntityTypeConfiguration<PgVectorEmbeddingEntity>
{
    public void Configure(EntityTypeBuilder<PgVectorEmbeddingEntity> builder)
    {
        builder.ToTable("pgvector_embeddings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever(); // PgVectorStoreAdapter assigns IDs externally

        builder.Property(e => e.VectorDocumentId)
            .IsRequired();

        builder.Property(e => e.GameId)
            .IsRequired();

        builder.Property(e => e.TextContent)
            .IsRequired();

        // vector(768) column — uses Pgvector.Vector type
        builder.Property(e => e.Vector)
            .IsRequired()
            .HasColumnType("vector(768)");

        builder.Property(e => e.Model)
            .IsRequired();

        builder.Property(e => e.ChunkIndex)
            .IsRequired();

        builder.Property(e => e.PageNumber)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // search_vector is a GENERATED ALWAYS AS (to_tsvector('english', text_content)) STORED column.
        // EF Core cannot manage generated stored columns via Fluent API without migration support,
        // so it is intentionally excluded from the mapping.

        builder.Property(e => e.Lang)
            .IsRequired()
            .HasMaxLength(5)
            .HasDefaultValue("en");

        builder.Property(e => e.SourceChunkId)
            .IsRequired(false);

        builder.Property(e => e.IsTranslation)
            .IsRequired()
            .HasDefaultValue(false);

        // Indexes for export/import query performance
        builder.HasIndex(e => e.VectorDocumentId);
        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => new { e.GameId, e.ChunkIndex });
    }
}
