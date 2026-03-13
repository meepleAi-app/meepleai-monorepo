using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for SessionParticipantEntity.
/// Table schema, indexes, and relationships for session participants (guest and registered).
/// </summary>
internal sealed class SessionParticipantEntityConfiguration : IEntityTypeConfiguration<SessionParticipantEntity>
{
    public void Configure(EntityTypeBuilder<SessionParticipantEntity> builder)
    {
        builder.ToTable("session_participants");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id");

        builder.Property(e => e.GuestName)
            .HasColumnName("guest_name")
            .HasMaxLength(100);

        builder.Property(e => e.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.AgentAccessEnabled)
            .HasColumnName("agent_access_enabled")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.ConnectionToken)
            .HasColumnName("connection_token")
            .HasMaxLength(6)
            .IsRequired();

        builder.Property(e => e.JoinedAt)
            .HasColumnName("joined_at")
            .IsRequired();

        builder.Property(e => e.LeftAt)
            .HasColumnName("left_at");

        // --- Indexes ---

        builder.HasIndex(e => e.SessionId)
            .HasDatabaseName("ix_session_participants_session_id");

        builder.HasIndex(e => e.ConnectionToken)
            .HasDatabaseName("ix_session_participants_connection_token")
            .IsUnique();

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_session_participants_user_id")
            .HasFilter("user_id IS NOT NULL");

        // --- Relationships ---

        builder.HasOne(e => e.LiveGameSession)
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
