using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicCardFeedbackEntity"/> (ADR-051 / #533 ME-M3.1).
/// </summary>
/// <remarks>
/// Enforces at DB level:
/// - Unique (card_id, user_id, claim_id): one feedback per user per claim → idempotent submit.
/// - CHECK error_type IN ('factual','ambiguous','contradicts_rule') OR NULL.
/// - Index (user_id, created_at) backs the per-user-per-day rate limit (#533 AC) and #534 aggregation.
/// - FK card_id → mechanic_cards ON DELETE CASCADE (feedback dies with its card).
/// </remarks>
internal sealed class MechanicCardFeedbackEntityConfiguration : IEntityTypeConfiguration<MechanicCardFeedbackEntity>
{
    public void Configure(EntityTypeBuilder<MechanicCardFeedbackEntity> builder)
    {
        builder.ToTable("mechanic_card_feedback", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_card_feedback_error_type",
                "error_type IS NULL OR error_type IN ('factual', 'ambiguous', 'contradicts_rule')");
        });

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id).HasColumnName("id").IsRequired();
        builder.Property(f => f.CardId).HasColumnName("card_id").IsRequired();
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.ClaimId).HasColumnName("claim_id").IsRequired();
        builder.Property(f => f.IsPositive).HasColumnName("is_positive").IsRequired();
        builder.Property(f => f.ErrorType).HasColumnName("error_type").HasMaxLength(32);
        builder.Property(f => f.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(f => f.SuggestedCitation).HasColumnName("suggested_citation").HasMaxLength(1000);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(f => new { f.CardId, f.UserId, f.ClaimId })
            .IsUnique()
            .HasDatabaseName("ux_mechanic_card_feedback_card_user_claim");

        builder.HasIndex(f => new { f.UserId, f.CreatedAt })
            .HasDatabaseName("ix_mechanic_card_feedback_user_created");

        builder.HasOne(f => f.Card)
            .WithMany()
            .HasForeignKey(f => f.CardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
