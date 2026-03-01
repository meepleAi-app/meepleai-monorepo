using Api.Infrastructure.Entities.DocumentProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.DocumentProcessing;

/// <summary>
/// EF Core configuration for the ProcessingJob entity and its owned entities.
/// Issue #4730: Processing queue management.
/// </summary>
internal class ProcessingJobEntityConfiguration : IEntityTypeConfiguration<ProcessingJobEntity>
{
    public void Configure(EntityTypeBuilder<ProcessingJobEntity> builder)
    {
        builder.ToTable("processing_jobs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PdfDocumentId)
            .IsRequired()
            .HasColumnName("pdf_document_id");

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnName("status")
            .HasDefaultValue("Queued");

        builder.Property(e => e.Priority)
            .IsRequired()
            .HasColumnName("priority")
            .HasDefaultValue(0);

        builder.Property(e => e.CurrentStep)
            .HasMaxLength(32)
            .HasColumnName("current_step");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000)
            .HasColumnName("error_message");

        builder.Property(e => e.RetryCount)
            .IsRequired()
            .HasColumnName("retry_count")
            .HasDefaultValue(0);

        builder.Property(e => e.MaxRetries)
            .IsRequired()
            .HasColumnName("max_retries")
            .HasDefaultValue(3);

        // Foreign keys
        builder.HasOne(e => e.PdfDocument)
            .WithMany()
            .HasForeignKey(e => e.PdfDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-many: Job → Steps
        builder.HasMany(e => e.Steps)
            .WithOne(e => e.ProcessingJob)
            .HasForeignKey(e => e.ProcessingJobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for queue queries
        builder.HasIndex(e => new { e.UserId, e.Status })
            .HasDatabaseName("ix_processing_jobs_user_status");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_processing_jobs_status");

        builder.HasIndex(e => new { e.Status, e.Priority, e.CreatedAt })
            .HasDatabaseName("ix_processing_jobs_status_priority_created");

        builder.HasIndex(e => e.PdfDocumentId)
            .HasDatabaseName("ix_processing_jobs_pdf_document");
    }
}

/// <summary>
/// EF Core configuration for the ProcessingStep entity.
/// Issue #4730: Processing queue management.
/// </summary>
internal class ProcessingStepEntityConfiguration : IEntityTypeConfiguration<ProcessingStepEntity>
{
    public void Configure(EntityTypeBuilder<ProcessingStepEntity> builder)
    {
        builder.ToTable("processing_steps");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProcessingJobId)
            .IsRequired()
            .HasColumnName("processing_job_id");

        builder.Property(e => e.StepName)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnName("step_name");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnName("status")
            .HasDefaultValue("Pending");

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(e => e.DurationMs)
            .HasColumnName("duration_ms");

        builder.Property(e => e.MetadataJson)
            .HasColumnName("metadata_json")
            .HasColumnType("jsonb");

        // One-to-many: Step → LogEntries
        builder.HasMany(e => e.LogEntries)
            .WithOne(e => e.ProcessingStep)
            .HasForeignKey(e => e.ProcessingStepId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ProcessingJobId)
            .HasDatabaseName("ix_processing_steps_job_id");
    }
}

/// <summary>
/// EF Core configuration for the StepLogEntry entity.
/// Issue #4730: Processing queue management.
/// </summary>
internal class StepLogEntryEntityConfiguration : IEntityTypeConfiguration<StepLogEntryEntity>
{
    public void Configure(EntityTypeBuilder<StepLogEntryEntity> builder)
    {
        builder.ToTable("step_log_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProcessingStepId)
            .IsRequired()
            .HasColumnName("processing_step_id");

        builder.Property(e => e.Timestamp)
            .IsRequired()
            .HasColumnName("timestamp");

        builder.Property(e => e.Level)
            .IsRequired()
            .HasMaxLength(16)
            .HasColumnName("level")
            .HasDefaultValue("Info");

        builder.Property(e => e.Message)
            .IsRequired()
            .HasMaxLength(4000)
            .HasColumnName("message");

        builder.HasIndex(e => e.ProcessingStepId)
            .HasDatabaseName("ix_step_log_entries_step_id");
    }
}
