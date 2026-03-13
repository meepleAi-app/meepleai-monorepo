using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

internal class ExtractedFactEntityConfiguration : IEntityTypeConfiguration<ExtractedFactEntity>
{
    public void Configure(EntityTypeBuilder<ExtractedFactEntity> builder)
    {
        builder.ToTable("extracted_facts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FactType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.FactData)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(e => e.ModelUsed)
            .HasMaxLength(100);

        builder.Property(e => e.Reviewer)
            .HasMaxLength(100);

        builder.HasIndex(e => e.GameId);
        builder.HasIndex(e => e.SourceDocumentId);
        builder.HasIndex(e => new { e.GameId, e.FactType });
        builder.HasIndex(e => e.IsReviewed);
    }
}
