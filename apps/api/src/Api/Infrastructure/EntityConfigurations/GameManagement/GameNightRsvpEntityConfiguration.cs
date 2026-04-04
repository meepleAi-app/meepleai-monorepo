using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for GameNightRsvpEntity.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
internal class GameNightRsvpEntityConfiguration : IEntityTypeConfiguration<GameNightRsvpEntity>
{
    public void Configure(EntityTypeBuilder<GameNightRsvpEntity> builder)
    {
        builder.ToTable("game_night_rsvps");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(r => r.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(r => r.RespondedAt).HasColumnName("responded_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(r => new { r.EventId, r.UserId })
            .HasDatabaseName("IX_game_night_rsvps_event_user")
            .IsUnique();

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_game_night_rsvps_user_id");
    }
}
