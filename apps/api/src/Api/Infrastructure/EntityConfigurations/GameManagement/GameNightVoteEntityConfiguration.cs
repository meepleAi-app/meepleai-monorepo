using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for GameNightVoteEntity.
/// Issue #2700: GameNight candidate voting (approval model).
/// </summary>
internal class GameNightVoteEntityConfiguration : IEntityTypeConfiguration<GameNightVoteEntity>
{
    public void Configure(EntityTypeBuilder<GameNightVoteEntity> builder)
    {
        builder.ToTable("game_night_votes");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(v => v.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(v => v.VoterUserId).HasColumnName("voter_user_id").IsRequired();
        builder.Property(v => v.CandidateGameId).HasColumnName("candidate_game_id").IsRequired();
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();

        // Approval uniqueness: one vote per (event, voter, candidate).
        builder.HasIndex(v => new { v.EventId, v.VoterUserId, v.CandidateGameId })
            .HasDatabaseName("IX_game_night_votes_event_voter_candidate")
            .IsUnique();

        // Tally lookups scan by candidate within an event.
        builder.HasIndex(v => new { v.EventId, v.CandidateGameId })
            .HasDatabaseName("IX_game_night_votes_event_candidate");
    }
}
