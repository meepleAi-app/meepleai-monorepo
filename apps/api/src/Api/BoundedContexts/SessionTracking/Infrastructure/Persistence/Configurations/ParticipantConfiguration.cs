using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity configuration for ParticipantEntity (persistence).
/// </summary>
public class ParticipantConfiguration : IEntityTypeConfiguration<ParticipantEntity>
{
    public void Configure(EntityTypeBuilder<ParticipantEntity> builder)
    {
        builder.ToTable("session_tracking_participants");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasColumnName("user_id");

        builder.Property(p => p.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.IsOwner)
            .HasColumnName("is_owner")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.JoinOrder)
            .HasColumnName("join_order")
            .IsRequired();

        builder.Property(p => p.FinalRank)
            .HasColumnName("final_rank");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Index for session participant lookup
        builder.HasIndex(p => p.SessionId)
            .HasDatabaseName("idx_participants_session");

        // Foreign key to session (cascade delete)
        builder.HasOne(p => p.Session)
            .WithMany(s => s.Participants)
            .HasForeignKey(p => p.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign key to users table (nullable)
        builder.HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}