using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TokenTier entity (Issue #3692)
/// </summary>
public sealed class TokenTierConfiguration : IEntityTypeConfiguration<TokenTier>
{
    public void Configure(EntityTypeBuilder<TokenTier> builder)
    {
        builder.ToTable("token_tiers", "administration");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(t => t.Name).IsUnique();

        // TierLimits value object (owned)
        builder.OwnsOne(t => t.Limits, limits =>
        {
            limits.Property(l => l.TokensPerMonth).HasColumnName("tokens_per_month").IsRequired();
            limits.Property(l => l.TokensPerDay).HasColumnName("tokens_per_day").IsRequired();
            limits.Property(l => l.MessagesPerDay).HasColumnName("messages_per_day").IsRequired();
            limits.Property(l => l.MaxCollectionSize).HasColumnName("max_collection_size").IsRequired();
            limits.Property(l => l.MaxPdfUploadsPerMonth).HasColumnName("max_pdf_uploads_per_month").IsRequired();
            // Issue #4138 (expand): MaxAgentsCreated is retired. The column stays as a shadow property
            // until a later delivery drops it. It is an int NOT NULL: making it nullable (as for
            // the retired string columns) would let new rows hold NULL, and the previous code
            // version — which maps a non-nullable int — would throw reading them after a rollback.
            // Measured: EF writes the shadow int as 0 on INSERT, so NOT NULL holds and the model
            // matches the snapshot — no migration (TierRetiredMaxAgentsColumnsTests).
            limits.Property<int>("MaxAgentsCreated").HasColumnName("max_agents_created").IsRequired();
            limits.Property(l => l.DailyCreditsLimit).HasColumnName("daily_credits_limit").HasColumnType("decimal(18,2)").IsRequired();
            limits.Property(l => l.WeeklyCreditsLimit).HasColumnName("weekly_credits_limit").HasColumnType("decimal(18,2)").IsRequired();
        });

        // TierPricing value object (owned)
        builder.OwnsOne(t => t.Pricing, pricing =>
        {
            pricing.Property(p => p.MonthlyFee).HasColumnName("monthly_fee").HasColumnType("decimal(10,2)").IsRequired();
            pricing.Property(p => p.CostPerExtraToken).HasColumnName("cost_per_extra_token").HasColumnType("decimal(10,6)");
            pricing.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
    }
}
