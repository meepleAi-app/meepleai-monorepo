using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SystemConfiguration;

/// <summary>
/// EF Core configuration for UserPreferences read-only projection.
/// Uses ToTable("users") with ExcludeFromMigrations to read from the users table.
/// Phase 6: Entity Decomposition — SystemConfiguration BC preferences projection.
/// </summary>
internal class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("users", t => t.ExcludeFromMigrations());
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Language).HasMaxLength(10);
        builder.Property(u => u.EmailNotifications);
        builder.Property(u => u.Theme).HasMaxLength(20);
        builder.Property(u => u.DataRetentionDays);
    }
}
