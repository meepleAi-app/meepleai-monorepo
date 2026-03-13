using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

internal class AnalysisFeedbackEntityConfiguration : IEntityTypeConfiguration<AnalysisFeedbackEntity>
{
    public void Configure(EntityTypeBuilder<AnalysisFeedbackEntity> builder)
    {
        builder.ToTable("analysis_feedback");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.AnalysisId)
            .IsRequired();

        builder.Property(e => e.Rating)
            .IsRequired();

        builder.Property(e => e.Comment)
            .HasMaxLength(2000);

        builder.Property(e => e.IsReviewed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.IsExported)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(e => new { e.UserId, e.AnalysisId })
            .IsUnique();

        builder.HasIndex(e => e.AnalysisId);
        builder.HasIndex(e => e.IsReviewed);
        builder.HasIndex(e => e.IsExported);
    }
}
