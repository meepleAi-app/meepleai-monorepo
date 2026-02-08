using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// EF Core configuration for TokenTier entity (Issue #3786)
/// </summary>
internal class TokenTierEntityConfiguration : IEntityTypeConfiguration<TokenTier>
{
    public void Configure(EntityTypeBuilder<TokenTier> builder)
    {
        builder.ToTable("token_tiers");

        // Primary Key
        builder.HasKey(e => e.Id);

        // Enum stored as string
        builder.Property(e => e.Name)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Complex type: TierLimits (owned entity)
        builder.OwnsOne(e => e.Limits, limits =>
        {
            limits.Property(l => l.TokensPerMonth).IsRequired();
            limits.Property(l => l.TokensPerDay).IsRequired();
            limits.Property(l => l.MessagesPerDay).IsRequired();
            limits.Property(l => l.MaxCollectionSize).IsRequired();
            limits.Property(l => l.MaxPdfUploadsPerMonth).IsRequired();
            limits.Property(l => l.MaxAgentsCreated).IsRequired();
        });

        // Complex type: TierPricing (owned entity)
        builder.OwnsOne(e => e.Pricing, pricing =>
        {
            pricing.Property(p => p.MonthlyFee)
                .IsRequired()
                .HasPrecision(18, 2);

            pricing.Property(p => p.CostPerExtraToken)
                .IsRequired(false)
                .HasPrecision(18, 6); // More precision for micro-costs

            pricing.Property(p => p.Currency)
                .IsRequired()
                .HasMaxLength(3); // ISO 4217 (EUR, USD)
        });

        // Properties
        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(e => e.Name)
            .IsUnique();

        builder.HasIndex(e => e.IsActive);
    }
}
