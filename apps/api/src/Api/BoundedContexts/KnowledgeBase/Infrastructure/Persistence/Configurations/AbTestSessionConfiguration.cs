using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for AbTestSession aggregate root.
/// Issue #5491: AbTestSession domain entity.
/// </summary>
public sealed class AbTestSessionConfiguration : IEntityTypeConfiguration<AbTestSession>
{
    public void Configure(EntityTypeBuilder<AbTestSession> builder)
    {
        builder.ToTable("ab_test_sessions", "knowledge_base");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(s => s.Query)
            .HasColumnName("query")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(s => s.KnowledgeBaseId)
            .HasColumnName("knowledge_base_id");

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.CompletedAt)
            .HasColumnName("completed_at");

        // #3651 lotto 5 — pattern xmin di ADR-060. La forma precedente (`byte[]` +
        // `IsRowVersion()`) generava una colonna `bytea NOT NULL` che Postgres non popola:
        // ogni INSERT su questa tabella falliva con 23502.
        builder.Property(s => s.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Relationship: Session has many Variants
        builder.HasMany(s => s.Variants)
            .WithOne()
            .HasForeignKey(v => v.AbTestSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: access private backing field
        builder.Navigation(s => s.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        builder.HasIndex(s => s.CreatedBy);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => s.CreatedAt);
    }
}
