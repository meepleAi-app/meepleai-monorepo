using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for DiceRollEntity (persistence, Phase 2 placeholder).
/// </summary>
public class DiceRollConfiguration : IEntityTypeConfiguration<DiceRollEntity>
{
    public void Configure(EntityTypeBuilder<DiceRollEntity> builder)
    {
        builder.ToTable("session_tracking_dice_rolls");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(d => d.ParticipantId)
            .HasColumnName("participant_id")
            .IsRequired();

        builder.Property(d => d.DiceType)
            .HasColumnName("dice_type")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(d => d.RollCount)
            .HasColumnName("roll_count")
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(d => d.Results)
            .HasColumnName("results")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(d => d.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        // Index for session roll history
        builder.HasIndex(d => new { d.SessionId, d.Timestamp })
            .HasDatabaseName("idx_dice_session")
            .IsDescending(false, true); // DESC on Timestamp

        // Foreign key to session (cascade delete)
        builder.HasOne(d => d.Session)
            .WithMany()
            .HasForeignKey(d => d.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign key to participant (cascade delete)
        builder.HasOne(d => d.Participant)
            .WithMany()
            .HasForeignKey(d => d.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}