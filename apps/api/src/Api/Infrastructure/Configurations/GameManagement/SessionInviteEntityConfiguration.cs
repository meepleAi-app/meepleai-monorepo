using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for SessionInviteEntity.
/// E3-1: Session invite table schema, indexes, and relationships.
/// </summary>
internal sealed class SessionInviteEntityConfiguration : IEntityTypeConfiguration<SessionInviteEntity>
{
    public void Configure(EntityTypeBuilder<SessionInviteEntity> builder)
    {
        builder.ToTable("session_invites");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(e => e.Pin).HasColumnName("pin").HasMaxLength(6).IsRequired();
        builder.Property(e => e.LinkToken).HasColumnName("link_token").HasMaxLength(32).IsRequired();
        builder.Property(e => e.MaxUses).HasColumnName("max_uses").IsRequired();
        builder.Property(e => e.CurrentUses).HasColumnName("current_uses").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(e => e.IsRevoked).HasColumnName("is_revoked").IsRequired().HasDefaultValue(false);

        builder.HasIndex(e => e.Pin).HasDatabaseName("ix_session_invites_pin");
        builder.HasIndex(e => e.LinkToken).HasDatabaseName("ix_session_invites_link_token").IsUnique();
        builder.HasIndex(e => e.SessionId).HasDatabaseName("ix_session_invites_session_id");

        builder.HasOne(e => e.LiveGameSession)
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
