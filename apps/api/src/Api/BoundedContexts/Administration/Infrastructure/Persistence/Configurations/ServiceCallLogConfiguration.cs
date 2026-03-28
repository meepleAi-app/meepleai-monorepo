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
        builder.Property(x => x.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(x => x.ServiceName).HasColumnName("service_name").IsRequired().HasMaxLength(100);
        builder.Property(x => x.HttpMethod).HasColumnName("http_method").IsRequired().HasMaxLength(10);
        builder.Property(x => x.RequestUrl).HasColumnName("request_url").IsRequired().HasMaxLength(2000);
        builder.Property(x => x.StatusCode).HasColumnName("status_code").IsRequired(false);
        builder.Property(x => x.LatencyMs).HasColumnName("latency_ms").IsRequired();
        builder.Property(x => x.IsSuccess).HasColumnName("is_success").IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").IsRequired(false).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").IsRequired(false).HasMaxLength(100);
        builder.Property(x => x.TimestampUtc).HasColumnName("timestamp_utc").IsRequired();
        builder.Property(x => x.RequestSummary).HasColumnName("request_summary").IsRequired(false).HasMaxLength(1000);
        builder.Property(x => x.ResponseSummary).HasColumnName("response_summary").IsRequired(false).HasMaxLength(1000);

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
