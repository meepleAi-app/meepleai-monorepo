using Api.Infrastructure.Entities.SystemConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SystemConfiguration;

/// <summary>
/// Entity Framework configuration for ShareRequestLimitConfig entity.
/// ISSUE-2730: Rate Limit Queries and Commands - Application Layer
/// </summary>
internal sealed class ShareRequestLimitConfigEntityConfiguration
    : IEntityTypeConfiguration<ShareRequestLimitConfigEntity>
{
    public void Configure(EntityTypeBuilder<ShareRequestLimitConfigEntity> builder)
    {
        builder.ToTable("share_request_limit_configs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.Tier)
            .HasColumnName("tier")
            .IsRequired();

        builder.Property(e => e.MaxPendingRequests)
            .HasColumnName("max_pending_requests")
            .IsRequired();

        builder.Property(e => e.MaxRequestsPerMonth)
            .HasColumnName("max_requests_per_month")
            .IsRequired();

        builder.Property(e => e.CooldownAfterRejectionSeconds)
            .HasColumnName("cooldown_after_rejection_seconds")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.Tier)
            .IsUnique()
            .HasFilter("is_active = true")
            .HasDatabaseName("ix_share_request_limit_configs_tier_unique_active");

        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("ix_share_request_limit_configs_is_active");
    }
}
