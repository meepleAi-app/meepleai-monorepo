using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for CardDrawEntity (persistence, Phase 2 placeholder).
/// </summary>
public class CardDrawConfiguration : IEntityTypeConfiguration<CardDrawEntity>
{
    public void Configure(EntityTypeBuilder<CardDrawEntity> builder)
    {
        builder.ToTable("session_tracking_card_draws");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(c => c.ParticipantId)
            .HasColumnName("participant_id")
            .IsRequired();

        builder.Property(c => c.DeckType)
            .HasColumnName("deck_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.DeckId)
            .HasColumnName("deck_id");

        builder.Property(c => c.CardValue)
            .HasColumnName("card_value")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        // Index for session card history
        builder.HasIndex(c => new { c.SessionId, c.Timestamp })
            .HasDatabaseName("idx_cards_session")
            .IsDescending(false, true); // DESC on Timestamp

        // Foreign key to session (cascade delete)
        builder.HasOne(c => c.Session)
            .WithMany()
            .HasForeignKey(c => c.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign key to participant (cascade delete)
        builder.HasOne(c => c.Participant)
            .WithMany()
            .HasForeignKey(c => c.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Note: DeckId FK will be added in Phase 2 when GameDeck entity is implemented
    }
}