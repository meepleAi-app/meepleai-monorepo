using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for ModelChangeLogEntity.
/// Issue #5496: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal class ModelChangeLogEntityConfiguration : IEntityTypeConfiguration<ModelChangeLogEntity>
{
    public void Configure(EntityTypeBuilder<ModelChangeLogEntity> builder)
    {
        builder.ToTable("model_change_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ModelId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ChangeType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.PreviousModelId)
            .HasMaxLength(200);

        builder.Property(e => e.NewModelId)
            .HasMaxLength(200);

        builder.Property(e => e.AffectedStrategy)
            .HasMaxLength(50);

        builder.Property(e => e.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.IsAutomatic)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.ChangedByUserId);

        builder.Property(e => e.OccurredAt)
            .IsRequired();

        // Indexes for query performance
        builder.HasIndex(e => e.ModelId);
        builder.HasIndex(e => e.ChangeType);
        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => e.AffectedStrategy);
    }
}
