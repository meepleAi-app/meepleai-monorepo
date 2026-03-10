using Api.BoundedContexts.Administration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// EF Core configuration for UserAiConsent entity (Issue #5512)
/// </summary>
internal class UserAiConsentEntityConfiguration : IEntityTypeConfiguration<UserAiConsent>
{
    public void Configure(EntityTypeBuilder<UserAiConsent> builder)
    {
        builder.ToTable("user_ai_consents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.ConsentedToAiProcessing)
            .IsRequired();

        builder.Property(e => e.ConsentedToExternalProviders)
            .IsRequired();

        builder.Property(e => e.ConsentedAt)
            .IsRequired();

        builder.Property(e => e.ConsentVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        // One consent record per user
        builder.HasIndex(e => e.UserId)
            .IsUnique();
    }
}
