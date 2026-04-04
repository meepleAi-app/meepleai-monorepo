using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

internal class SimilarityAuditResultEntityConfiguration : IEntityTypeConfiguration<SimilarityAuditResultEntity>
{
    public void Configure(EntityTypeBuilder<SimilarityAuditResultEntity> builder)
    {
        builder.ToTable("similarity_audit_results");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SourceGame)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.CheckName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Score)
            .HasPrecision(5, 4);

        builder.Property(e => e.Threshold)
            .HasPrecision(5, 4);

        builder.HasIndex(e => e.PairId);
        builder.HasIndex(e => e.SourceGame);
        builder.HasIndex(e => new { e.PairId, e.CheckName });
        builder.HasIndex(e => e.Passed);
    }
}
