using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// Entity Framework configuration for ApiKeyUsageLogEntity.
/// </summary>
public class ApiKeyUsageLogEntityConfiguration : IEntityTypeConfiguration<ApiKeyUsageLogEntity>
{
    public void Configure(EntityTypeBuilder<ApiKeyUsageLogEntity> builder)
    {
        builder.ToTable("api_key_usage_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.KeyId)
            .HasColumnName("key_id")
            .IsRequired();

        builder.Property(e => e.UsedAt)
            .HasColumnName("used_at")
            .IsRequired();

        builder.Property(e => e.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45); // IPv6 max length

        builder.Property(e => e.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(e => e.HttpMethod)
            .HasColumnName("http_method")
            .HasMaxLength(10);

        builder.Property(e => e.StatusCode)
            .HasColumnName("status_code");

        builder.Property(e => e.ResponseTimeMs)
            .HasColumnName("response_time_ms");

        // Indexes for query performance
        builder.HasIndex(e => new { e.KeyId, e.UsedAt })
            .HasDatabaseName("ix_api_key_usage_logs_key_id_used_at")
            .IsDescending(false, true); // Descending on UsedAt for recent-first queries

        builder.HasIndex(e => e.KeyId)
            .HasDatabaseName("ix_api_key_usage_logs_key_id");

        builder.HasIndex(e => e.UsedAt)
            .HasDatabaseName("ix_api_key_usage_logs_used_at");

        // Foreign key relationship
        builder.HasOne(e => e.ApiKey)
            .WithMany()
            .HasForeignKey(e => e.KeyId)
            .OnDelete(DeleteBehavior.Cascade); // Delete logs when API key is deleted
    }
}
