using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for GameNightEventEntity.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
internal class GameNightEventEntityConfiguration : IEntityTypeConfiguration<GameNightEventEntity>
{
    public void Configure(EntityTypeBuilder<GameNightEventEntity> builder)
    {
        builder.ToTable("game_night_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.OrganizerId).HasColumnName("organizer_id").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(e => e.ScheduledAt).HasColumnName("scheduled_at").IsRequired();
        builder.Property(e => e.Location).HasColumnName("location").HasMaxLength(500);
        builder.Property(e => e.MaxPlayers).HasColumnName("max_players");
        builder.Property(e => e.GameIdsJson).HasColumnName("game_ids").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Reminder24hSentAt).HasColumnName("reminder_24h_sent_at");
        builder.Property(e => e.Reminder1hSentAt).HasColumnName("reminder_1h_sent_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.OrganizerId, e.ScheduledAt })
            .HasDatabaseName("IX_game_night_events_organizer_scheduled");

        builder.HasIndex(e => new { e.Status, e.ScheduledAt })
            .HasDatabaseName("IX_game_night_events_status_scheduled");

        builder.HasMany(e => e.Rsvps)
            .WithOne(r => r.Event)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
