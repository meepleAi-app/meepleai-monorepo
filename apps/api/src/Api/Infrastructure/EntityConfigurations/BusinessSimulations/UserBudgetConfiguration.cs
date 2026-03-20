using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.BusinessSimulations;

/// <summary>
/// EF Core configuration for UserBudget read-only projection.
/// Uses ToTable("users") with ExcludeFromMigrations to read from the users table.
/// Phase 6: Entity Decomposition — BusinessSimulations BC budget projection.
/// </summary>
internal class UserBudgetConfiguration : IEntityTypeConfiguration<UserBudget>
{
    public void Configure(EntityTypeBuilder<UserBudget> builder)
    {
        // ToTable with ExcludeFromMigrations instead of ToView to avoid
        // EF Core conflict when multiple entities map to the same view.
        // All three projections (UserProfile, UserBudget, UserPreferences)
        // share the "users" table with the same PK (Id).
        builder.ToTable("users", t => t.ExcludeFromMigrations());
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Tier);
        builder.Property(u => u.Level);
        builder.Property(u => u.ExperiencePoints);
        builder.Property(u => u.IsContributor);
    }
}
