using Api.BoundedContexts.Administration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for BatchJob entity (Issue #3693)
/// </summary>
internal sealed class BatchJobConfiguration : IEntityTypeConfiguration<BatchJob>
{
    public void Configure(EntityTypeBuilder<BatchJob> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("batch_jobs", schema: "administration");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Parameters)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.Progress)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.StartedAt)
            .IsRequired(false);

        builder.Property(x => x.CompletedAt)
            .IsRequired(false);

        builder.Property(x => x.DurationSeconds)
            .IsRequired(false);

        builder.Property(x => x.ResultData)
            .IsRequired(false)
            .HasColumnType("jsonb");

        builder.Property(x => x.ResultSummary)
            .IsRequired(false)
            .HasMaxLength(2000);

        builder.Property(x => x.OutputFileUrl)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(x => x.ErrorMessage)
            .IsRequired(false)
            .HasMaxLength(2000);

        builder.Property(x => x.ErrorStack)
            .IsRequired(false);

        builder.Property(x => x.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedBy)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_batch_jobs_status");

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("ix_batch_jobs_created_at");

        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("ix_batch_jobs_status_created_at");
    }
}
