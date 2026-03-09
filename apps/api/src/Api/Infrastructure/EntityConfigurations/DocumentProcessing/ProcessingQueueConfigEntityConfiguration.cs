using Api.Infrastructure.Entities.DocumentProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.DocumentProcessing;

/// <summary>
/// EF Core configuration for the ProcessingQueueConfig singleton entity.
/// Issue #5455: Queue configuration management.
/// </summary>
internal class ProcessingQueueConfigEntityConfiguration : IEntityTypeConfiguration<ProcessingQueueConfigEntity>
{
    public void Configure(EntityTypeBuilder<ProcessingQueueConfigEntity> builder)
    {
        builder.ToTable("processing_queue_config");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.IsPaused)
            .IsRequired()
            .HasColumnName("is_paused")
            .HasDefaultValue(false);

        builder.Property(e => e.MaxConcurrentWorkers)
            .IsRequired()
            .HasColumnName("max_concurrent_workers")
            .HasDefaultValue(3);

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by");
    }
}
