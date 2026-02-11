using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

/// <summary>
/// Entity configuration for GameAnalyticsEventEntity.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
internal class GameAnalyticsEventEntityConfiguration : IEntityTypeConfiguration<GameAnalyticsEventEntity>
{
    public void Configure(EntityTypeBuilder<GameAnalyticsEventEntity> builder)
    {
        builder.ToTable("game_analytics_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.GameId)
            .HasColumnName("game_id")
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id");

        builder.Property(e => e.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        // Index for trending calculation: filter by timestamp, group by game_id
        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("ix_game_analytics_events_timestamp");

        builder.HasIndex(e => new { e.GameId, e.Timestamp })
            .HasDatabaseName("ix_game_analytics_events_game_id_timestamp");
    }
}
