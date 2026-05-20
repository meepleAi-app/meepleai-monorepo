using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SessionTracking;

/// <summary>
/// EF Core configuration for GamebookCampaignSession.
/// Uses direct domain entity mapping (no separate persistence entity).
/// Maps the GamebookProgress value object to a single jsonb column via System.Text.Json.
/// Phase A0.1 (2026-05-19): moved from BoundedContexts/SessionTracking/Infrastructure/Persistence/Configurations
/// to canonical Infrastructure/EntityConfigurations location to align with the rest of the codebase.
/// Iter 1.A — Libro Game Nanolith dogfood demo.
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

        builder.Property(e => e.Progress)
            .HasColumnName("progress")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(
                    new ProgressDto(v.CurrentParagraph, v.History.ToArray(), v.LastReadAt),
                    (JsonSerializerOptions?)null),
                v => DeserializeProgress(v));

        // A0.2 (#1320): index over (owner_user_id, game_ref_kind, game_ref_id, is_deleted) is
        // declared via raw SQL in the migration because composite indexes spanning both the
        // parent entity (owner_user_id, is_deleted) AND owned-type columns (game_ref_kind,
        // game_ref_id) cannot be expressed cleanly via HasIndex(...) in EF Core 9 — owned-type
        // properties are not addressable from the parent EntityTypeBuilder.
        // The old `ix_gamebook_campaign_sessions_owner_game` index (on game_id) is dropped in
        // the same migration. See migration RefactorGamebookGameIdToGameRef.

        builder.HasQueryFilter(e => !e.IsDeleted);
    }

    private static GamebookProgress DeserializeProgress(string json)
    {
        var dto = JsonSerializer.Deserialize<ProgressDto>(json)
            ?? new ProgressDto(0, Array.Empty<int>(), DateTimeOffset.UtcNow);
        return GamebookProgress.Create(dto.CurrentParagraph, dto.History);
    }

    private sealed record ProgressDto(int CurrentParagraph, int[] History, DateTimeOffset LastReadAt);
}
