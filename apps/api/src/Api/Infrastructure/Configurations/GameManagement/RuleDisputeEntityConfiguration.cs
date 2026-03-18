using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for RuleDisputeEntity.
/// Table schema, indexes, and relationships for structured rule disputes.
/// </summary>
internal sealed class RuleDisputeEntityConfiguration : IEntityTypeConfiguration<RuleDisputeEntity>
{
    public void Configure(EntityTypeBuilder<RuleDisputeEntity> builder)
    {
        builder.ToTable("rule_disputes");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(e => e.GameId)
            .HasColumnName("game_id")
            .IsRequired();

        builder.Property(e => e.InitiatorPlayerId)
            .HasColumnName("initiator_player_id")
            .IsRequired();

        builder.Property(e => e.RespondentPlayerId)
            .HasColumnName("respondent_player_id");

        builder.Property(e => e.InitiatorClaim)
            .HasColumnName("initiator_claim")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.RespondentClaim)
            .HasColumnName("respondent_claim")
            .HasMaxLength(2000);

        builder.Property(e => e.FinalOutcome)
            .HasColumnName("final_outcome")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.OverrideRule)
            .HasColumnName("override_rule")
            .HasMaxLength(2000);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // --- JSON Columns ---

        builder.Property(e => e.VerdictJson)
            .HasColumnName("verdict_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.VotesJson)
            .HasColumnName("votes_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.RelatedDisputeIdsJson)
            .HasColumnName("related_dispute_ids_json")
            .HasColumnType("jsonb");

        // --- Indexes ---

        builder.HasIndex(e => e.SessionId)
            .HasDatabaseName("ix_rule_disputes_session_id");

        builder.HasIndex(e => e.GameId)
            .HasDatabaseName("ix_rule_disputes_game_id");

        builder.HasIndex(e => new { e.GameId, e.CreatedAt })
            .HasDatabaseName("ix_rule_disputes_game_id_created_at");

        // --- Relationships ---

        builder.HasOne(e => e.Session)
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
