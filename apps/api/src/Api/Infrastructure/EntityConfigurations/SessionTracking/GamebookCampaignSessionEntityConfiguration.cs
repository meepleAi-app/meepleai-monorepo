using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SessionTracking;

/// <summary>
/// EF Core configuration for GamebookCampaignSession.
/// Uses direct domain entity mapping (no separate persistence entity).
/// Phase A0.1 (2026-05-19): moved from BoundedContexts/SessionTracking/Infrastructure/Persistence/Configurations
/// to canonical Infrastructure/EntityConfigurations location to align with the rest of the codebase.
/// C2 (2026-05-19): the per-campaign progress value object was removed; per-book progress now
/// lives in <see cref="SessionBookProgress"/> (one row per campaign+book pair). The legacy
/// <c>progress</c> jsonb column on this table is dropped in migration <c>RemoveGamebookProgressVO</c>.
/// </summary>
internal class GamebookCampaignSessionEntityConfiguration : IEntityTypeConfiguration<GamebookCampaignSession>
{
    public void Configure(EntityTypeBuilder<GamebookCampaignSession> builder)
    {
        builder.ToTable("gamebook_campaign_sessions", schema: "session_tracking");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        // A0.2 (#1320): GameRef discriminator (owned value object → 2 scalar columns).
        // Replaces bare `Guid GameId` to align with the cross-BC GameRef refactor.
        builder.OwnsOne(e => e.GameRef, gr =>
        {
            gr.Property(p => p.Id).HasColumnName("game_ref_id").IsRequired();
            gr.Property(p => p.Kind).HasColumnName("game_ref_kind").HasConversion<short>().IsRequired();
        });
        builder.Property(e => e.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");

        // A0.2 (#1320): index over (owner_user_id, game_ref_kind, game_ref_id, is_deleted) is
        // declared via raw SQL in the migration because composite indexes spanning both the
        // parent entity (owner_user_id, is_deleted) AND owned-type columns (game_ref_kind,
        // game_ref_id) cannot be expressed cleanly via HasIndex(...) in EF Core 9 — owned-type
        // properties are not addressable from the parent EntityTypeBuilder.
        // The old `ix_gamebook_campaign_sessions_owner_game` index (on game_id) is dropped in
        // the same migration. See migration RefactorGamebookGameIdToGameRef.

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
