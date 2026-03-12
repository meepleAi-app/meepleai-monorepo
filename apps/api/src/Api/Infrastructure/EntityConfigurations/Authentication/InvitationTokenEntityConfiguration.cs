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

        // Indexes
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => new { e.Email, e.Status });
        builder.HasIndex(e => e.ExpiresAt);

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
    }
}
