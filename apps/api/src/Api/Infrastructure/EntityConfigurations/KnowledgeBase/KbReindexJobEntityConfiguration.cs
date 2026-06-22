using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for <see cref="KbReindexJobEntity"/>.
/// Issue #941 / ADR-057.
/// </summary>
internal class KbReindexJobEntityConfiguration : IEntityTypeConfiguration<KbReindexJobEntity>
{
    public void Configure(EntityTypeBuilder<KbReindexJobEntity> builder)
    {
        builder.ToTable("kb_reindex_jobs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.GameId).IsRequired();
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.Status).IsRequired().HasMaxLength(32);
        builder.Property(e => e.TotalPdfs).IsRequired();
        builder.Property(e => e.ProcessedPdfs).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.FailureReason).HasMaxLength(2048);

        // Index for the concurrency-invariant query (active job by game+user)
        builder.HasIndex(e => new { e.GameId, e.UserId, e.Status });
        // Index for status polling (read-by-id is already by PK)
        builder.HasIndex(e => e.CreatedAt);
    }
}
