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

        // RSVP deadline — ADR-074 (#2383)
        builder.Property(e => e.RsvpDeadline).HasColumnName("rsvp_deadline");
        builder.Property(e => e.RsvpClosedAt).HasColumnName("rsvp_closed_at");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // Optimistic concurrency via PostgreSQL's xmin system column (Issue #2703, ADR-060).
        // Server-owned, collision-safe (xmin = unique transaction id per row UPDATE).
        // Mirrors game_night_playlists (#2306) — no bytea row_version, no trigger.
        builder.Property(e => e.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(e => new { e.OrganizerId, e.ScheduledAt })
            .HasDatabaseName("IX_game_night_events_organizer_scheduled");

        builder.HasIndex(e => new { e.Status, e.ScheduledAt })
            .HasDatabaseName("IX_game_night_events_status_scheduled");

        builder.HasMany(e => e.Rsvps)
            .WithOne(r => r.Event)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Sessions)
            .WithOne(s => s.GameNightEvent)
            .HasForeignKey(s => s.GameNightEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
