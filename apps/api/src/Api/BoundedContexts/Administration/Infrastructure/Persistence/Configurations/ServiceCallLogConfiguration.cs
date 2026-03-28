using Api.BoundedContexts.Administration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence.Configurations;

internal sealed class ServiceCallLogConfiguration : IEntityTypeConfiguration<ServiceCallLogEntry>
{
    public void Configure(EntityTypeBuilder<ServiceCallLogEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("service_call_logs", schema: "administration");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired().ValueGeneratedNever();
        builder.Property(x => x.ServiceName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.HttpMethod).IsRequired().HasMaxLength(10);
        builder.Property(x => x.RequestUrl).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.StatusCode).IsRequired(false);
        builder.Property(x => x.LatencyMs).IsRequired();
        builder.Property(x => x.IsSuccess).IsRequired();
        builder.Property(x => x.ErrorMessage).IsRequired(false).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).IsRequired(false).HasMaxLength(100);
        builder.Property(x => x.TimestampUtc).IsRequired();
        builder.Property(x => x.RequestSummary).IsRequired(false).HasMaxLength(1000);
        builder.Property(x => x.ResponseSummary).IsRequired(false).HasMaxLength(1000);

        builder.HasIndex(x => x.TimestampUtc)
            .HasDatabaseName("ix_service_call_logs_timestamp")
            .IsDescending();

        builder.HasIndex(x => x.ServiceName)
            .HasDatabaseName("ix_service_call_logs_service_name");

        builder.HasIndex(x => new { x.ServiceName, x.TimestampUtc })
            .HasDatabaseName("ix_service_call_logs_service_timestamp");

        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("ix_service_call_logs_correlation_id");

        builder.HasIndex(x => x.IsSuccess)
            .HasDatabaseName("ix_service_call_logs_is_success");
    }
}
