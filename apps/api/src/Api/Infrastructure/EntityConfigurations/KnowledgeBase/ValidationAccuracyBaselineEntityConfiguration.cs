using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// Entity configuration for ValidationAccuracyBaselineEntity.
/// BGAI-039: Validation accuracy baseline measurement tracking.
/// </summary>
internal class ValidationAccuracyBaselineEntityConfiguration : IEntityTypeConfiguration<ValidationAccuracyBaselineEntity>
{
    public void Configure(EntityTypeBuilder<ValidationAccuracyBaselineEntity> builder)
    {
        builder.ToTable("validation_accuracy_baselines");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Context)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.DatasetId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.EvaluationId)
            .IsRequired(false);

        builder.Property(e => e.MeasuredAt)
            .IsRequired();

        builder.Property(e => e.TruePositives)
            .IsRequired();

        builder.Property(e => e.TrueNegatives)
            .IsRequired();

        builder.Property(e => e.FalsePositives)
            .IsRequired();

        builder.Property(e => e.FalseNegatives)
            .IsRequired();

        builder.Property(e => e.TotalCases)
            .IsRequired();

        builder.Property(e => e.Precision)
            .IsRequired()
            .HasPrecision(5, 4); // e.g., 0.9876

        builder.Property(e => e.Recall)
            .IsRequired()
            .HasPrecision(5, 4);

        builder.Property(e => e.F1Score)
            .IsRequired()
            .HasPrecision(5, 4);

        builder.Property(e => e.Accuracy)
            .IsRequired()
            .HasPrecision(5, 4);

        builder.Property(e => e.Specificity)
            .IsRequired()
            .HasPrecision(5, 4);

        builder.Property(e => e.MatthewsCorrelation)
            .IsRequired()
            .HasPrecision(6, 4); // Range: -1.0 to 1.0

        builder.Property(e => e.MeetsBaseline)
            .IsRequired();

        builder.Property(e => e.QualityLevel)
            .IsRequired();

        builder.Property(e => e.Summary)
            .HasMaxLength(500);

        builder.Property(e => e.RecommendationsJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt);

        // Indexes for common queries
        builder.HasIndex(e => e.Context)
            .HasDatabaseName("ix_validation_accuracy_baselines_context");

        builder.HasIndex(e => e.DatasetId)
            .HasDatabaseName("ix_validation_accuracy_baselines_dataset_id");

        builder.HasIndex(e => e.MeasuredAt)
            .HasDatabaseName("ix_validation_accuracy_baselines_measured_at");

        builder.HasIndex(e => e.Accuracy)
            .HasDatabaseName("ix_validation_accuracy_baselines_accuracy");

        builder.HasIndex(e => e.MeetsBaseline)
            .HasDatabaseName("ix_validation_accuracy_baselines_meets_baseline");

        // Foreign key to prompt_evaluation_results (optional)
        builder.HasIndex(e => e.EvaluationId)
            .HasDatabaseName("ix_validation_accuracy_baselines_evaluation_id");
    }
}
