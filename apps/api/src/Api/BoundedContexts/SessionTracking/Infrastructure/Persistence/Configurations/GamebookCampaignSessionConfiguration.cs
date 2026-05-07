using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for GamebookCampaignSession.
/// Uses direct domain entity mapping (no separate persistence entity).
/// Iter 1.A — Libro Game Nanolith dogfood demo.
/// </summary>
internal sealed class GamebookCampaignSessionConfiguration : IEntityTypeConfiguration<GamebookCampaignSession>
{
    public void Configure(EntityTypeBuilder<GamebookCampaignSession> builder)
    {
        builder.ToTable("gamebook_campaign_sessions", schema: "session_tracking");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.GameId).HasColumnName("game_id").IsRequired();
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
            .HasConversion(
                v => JsonSerializer.Serialize(
                    new ProgressDto(v.CurrentParagraph, v.History.ToArray(), v.LastReadAt),
                    (JsonSerializerOptions?)null),
                v => DeserializeProgress(v));

        builder.HasIndex(e => new { e.OwnerUserId, e.GameId, e.IsDeleted })
            .HasDatabaseName("ix_gamebook_campaign_sessions_owner_game");

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
