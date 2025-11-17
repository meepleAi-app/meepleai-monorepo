using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

// ADMIN-01 Phase 4: Prompt Evaluation Results
public class PromptEvaluationResultEntityConfiguration : IEntityTypeConfiguration<PromptEvaluationResultEntity>
{
    public void Configure(EntityTypeBuilder<PromptEvaluationResultEntity> builder)
    {
        builder.ToTable("prompt_evaluation_results");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(100);
        builder.Property(e => e.TemplateId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.VersionId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.DatasetId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.ExecutedAt).IsRequired();
        builder.Property(e => e.TotalQueries).IsRequired();
        // BGAI-041: 5-metric quality evaluation framework
        builder.Property(e => e.Accuracy).IsRequired();
        builder.Property(e => e.Relevance).IsRequired();
        builder.Property(e => e.Completeness).IsRequired();
        builder.Property(e => e.Clarity).IsRequired();
        builder.Property(e => e.CitationQuality).IsRequired();
        builder.Property(e => e.Passed).IsRequired();
        builder.Property(e => e.Summary).HasMaxLength(500);
        builder.Property(e => e.QueryResultsJson).HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        // Indexes
        builder.HasIndex(e => e.TemplateId);
        builder.HasIndex(e => e.VersionId);
        builder.HasIndex(e => e.ExecutedAt);
        builder.HasIndex(e => new { e.TemplateId, e.VersionId, e.ExecutedAt });
    }
}
