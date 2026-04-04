using Api.BoundedContexts.Administration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// EF Core configuration for UserProfile read-only projection.
/// Uses ToTable("users") with ExcludeFromMigrations to read from the users table.
/// Phase 6: Entity Decomposition — Administration BC profile projection.
/// </summary>
internal class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        // Each read-only projection needs its own unique view name.
        // The view "vw_user_profiles" is created at startup via EnsureUserProjectionViews.
        builder.ToView("vw_user_profiles");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.DisplayName).HasMaxLength(128);
        builder.Property(u => u.AvatarUrl).HasMaxLength(2048);
        builder.Property(u => u.Bio).HasMaxLength(500);
        builder.Property(u => u.Role).HasMaxLength(32);
        builder.Property(u => u.Tier);
        builder.Property(u => u.Status);
        builder.Property(u => u.CreatedAt);
        builder.Property(u => u.Level);
        builder.Property(u => u.ExperiencePoints);
        builder.Property(u => u.EmailVerified);
        builder.Property(u => u.IsTwoFactorEnabled);
        builder.Property(u => u.IsSuspended);
        builder.Property(u => u.IsContributor);
        builder.Property(u => u.IsDemoAccount);
    }
}
