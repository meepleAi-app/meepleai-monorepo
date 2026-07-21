using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

// AI-14: Hybrid search - text chunks table configuration
internal class TextChunkEntityConfiguration : IEntityTypeConfiguration<TextChunkEntity>
{
    public void Configure(EntityTypeBuilder<TextChunkEntity> builder)
    {
        builder.ToTable("text_chunks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.GameId).IsRequired(false).HasMaxLength(64);
        builder.Property(e => e.SharedGameId).IsRequired(false);
        builder.Property(e => e.PdfDocumentId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Content).IsRequired(); // No max length for long text chunks
        builder.Property(e => e.ChunkIndex).IsRequired();
        builder.Property(e => e.PageNumber);
        builder.Property(e => e.CharacterCount).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        // Issue #730: Chunk hierarchy fields
        builder.Property(e => e.Heading).HasMaxLength(500).IsRequired(false);
        builder.Property(e => e.ParentChunkId).IsRequired(false);
        // #3281: ValueGeneratedNever() so EF always sends the explicit CLR Level, even Level=0
        // (parent/section chunks). Without it, HasDefaultValue(1) marks the property ValueGeneratedOnAdd,
        // and EF omits it from INSERT when the value equals the CLR default (0 for short) → Npgsql
        // substitutes the store default 1, silently corrupting Level-0 parent rows to Level-1. The
        // CLR default of TextChunkEntity.Level is 1, so creators that never set it still persist 1.
        builder.Property(e => e.Level).IsRequired().HasDefaultValue<short>(1).ValueGeneratedNever();
        builder.Property(e => e.ElementType).IsRequired().HasMaxLength(20).HasDefaultValue("NarrativeText");

        // #2311 BE-1 — UsageCount counter (DEC-D2 start-from-0)
        builder.Property(e => e.UsageCount)
               .HasColumnName("usage_count")
               .IsRequired()
               .HasDefaultValue(0);

        // Phase D — RAG role-aware: multi-label role classification (bitflag)
        builder.Property(e => e.RoleTags)
               .HasColumnName("role_tags")
               .HasConversion<int>()
               .HasDefaultValue(GameBookRole.None)
               .IsRequired();
        builder.HasIndex(e => e.RoleTags)
               .HasDatabaseName("ix_text_chunks_role_tags")
               .HasFilter("role_tags != 0");

        builder.HasIndex(e => e.ParentChunkId);
        builder.HasIndex(e => new { e.PdfDocumentId, e.ChunkIndex }).HasDatabaseName("ix_text_chunks_pdf_chunk_index");

        // AI-14: Hybrid search - PostgreSQL GENERATED stored tsvector column
        // Computed from "Content" via migration AddSearchVectorColumns. Ignored by EF Core since it's DB-managed.
        builder.Ignore(e => e.SearchVector);

        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.PdfDocument)
            .WithMany()
            .HasForeignKey(e => e.PdfDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => e.SharedGameId);
        builder.HasIndex(e => e.PdfDocumentId);
        builder.HasIndex(e => e.ChunkIndex);
        builder.HasIndex(e => e.PageNumber);
    }
}
