using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.BusinessSimulations;

/// <summary>
/// EF Core configuration for UserBudget read-only projection.
/// Uses ToView("users") to map to the existing users table without generating migrations.
/// Phase 6: Entity Decomposition — BusinessSimulations BC budget projection.
/// </summary>
internal class UserBudgetConfiguration : IEntityTypeConfiguration<UserBudget>
{
    public void Configure(EntityTypeBuilder<UserBudget> builder)
    {
        builder.ToView("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Tier);
        builder.Property(u => u.Level);
        builder.Property(u => u.ExperiencePoints);
        builder.Property(u => u.IsContributor);
    }
}
