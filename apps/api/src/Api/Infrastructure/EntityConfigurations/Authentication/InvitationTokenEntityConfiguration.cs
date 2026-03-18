using Api.Infrastructure.Entities.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class InvitationTokenEntityConfiguration : IEntityTypeConfiguration<InvitationTokenEntity>
{
    public void Configure(EntityTypeBuilder<InvitationTokenEntity> builder)
    {
        builder.ToTable("invitation_tokens");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Email).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Role).IsRequired().HasMaxLength(32);
        builder.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(16);
        builder.Property(e => e.InvitedByUserId).IsRequired();
        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.AcceptedAt);
        builder.Property(e => e.AcceptedByUserId);
        builder.Property(e => e.RevokedAt);

        // Admin Invitation Flow: custom message and pending user
        builder.Property(e => e.CustomMessage).HasMaxLength(500).IsRequired(false);
        builder.Property(e => e.PendingUserId).IsRequired(false);

        // Indexes
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => new { e.Email, e.Status });
        builder.HasIndex(e => e.ExpiresAt);
        builder.HasIndex(e => e.PendingUserId)
            .HasDatabaseName("IX_InvitationTokens_PendingUserId")
            .HasFilter("pending_user_id IS NOT NULL");

        // Relationships — FK to users table
        builder.HasOne(e => e.InvitedByUser)
            .WithMany()
            .HasForeignKey(e => e.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AcceptedByUser)
            .WithMany()
            .HasForeignKey(e => e.AcceptedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.PendingUser)
            .WithMany()
            .HasForeignKey(e => e.PendingUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // GameSuggestions — cascade delete when invitation is removed
        builder.HasMany(e => e.GameSuggestions)
            .WithOne(gs => gs.InvitationToken)
            .HasForeignKey(gs => gs.InvitationTokenId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
