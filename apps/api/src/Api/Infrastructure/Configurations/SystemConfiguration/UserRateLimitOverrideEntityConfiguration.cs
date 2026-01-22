using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SystemConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SystemConfiguration;

/// <summary>
/// Entity Framework configuration for UserRateLimitOverride entity.
/// ISSUE-2730: Rate Limit Queries and Commands - Application Layer
/// </summary>
internal sealed class UserRateLimitOverrideEntityConfiguration
    : IEntityTypeConfiguration<UserRateLimitOverrideEntity>
{
    public void Configure(EntityTypeBuilder<UserRateLimitOverrideEntity> builder)
    {
        builder.ToTable("user_rate_limit_overrides");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.MaxPendingRequests)
            .HasColumnName("max_pending_requests");

        builder.Property(e => e.MaxRequestsPerMonth)
            .HasColumnName("max_requests_per_month");

        builder.Property(e => e.CooldownAfterRejectionSeconds)
            .HasColumnName("cooldown_after_rejection_seconds");

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.CreatedByAdminId)
            .HasColumnName("created_by_admin_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasFilter("expires_at IS NULL OR expires_at > NOW()")
            .HasDatabaseName("ix_user_rate_limit_overrides_user_id_active");

        builder.HasIndex(e => e.CreatedByAdminId)
            .HasDatabaseName("ix_user_rate_limit_overrides_created_by_admin_id");

        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_user_rate_limit_overrides_expires_at")
            .HasFilter("expires_at IS NOT NULL");

        // Foreign Keys
        builder.HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
