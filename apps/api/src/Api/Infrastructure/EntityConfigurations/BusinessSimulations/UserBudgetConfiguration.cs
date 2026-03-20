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
        // Each read-only projection needs its own unique view name to avoid
        // EF Core conflicts when multiple entities map to the same underlying data.
        // The view "vw_user_budgets" is created at startup via EnsureUserProjectionViews.
        builder.ToView("vw_user_budgets");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Tier);
        builder.Property(u => u.Level);
        builder.Property(u => u.ExperiencePoints);
        builder.Property(u => u.IsContributor);
    }
}
