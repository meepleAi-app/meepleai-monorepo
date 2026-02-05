using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for DiceRollEntity (persistence).
/// Maps to session_tracking_dice_rolls table.
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

        builder.Property(d => d.Formula)
            .HasColumnName("formula")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.Label)
            .HasColumnName("label")
            .HasMaxLength(100);

        builder.Property(d => d.Rolls)
            .HasColumnName("rolls")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(d => d.Modifier)
            .HasColumnName("modifier")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(d => d.Total)
            .HasColumnName("total")
            .IsRequired();

        builder.Property(d => d.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(d => d.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(d => d.DeletedAt)
            .HasColumnName("deleted_at");

        // Soft delete query filter
        builder.HasQueryFilter(d => !d.IsDeleted);

        // Index for session roll history (ordered by timestamp desc)
        builder.HasIndex(d => new { d.SessionId, d.Timestamp })
            .HasDatabaseName("idx_dice_session_timestamp")
            .IsDescending(false, true);

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
