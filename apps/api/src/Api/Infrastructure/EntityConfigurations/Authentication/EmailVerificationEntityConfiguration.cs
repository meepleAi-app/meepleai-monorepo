using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

// ISSUE-3071: Email verification tokens
internal class EmailVerificationEntityConfiguration : IEntityTypeConfiguration<EmailVerificationEntity>
{
    public void Configure(EntityTypeBuilder<EmailVerificationEntity> builder)
    {
        builder.ToTable("email_verifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.TokenHash).IsRequired().HasMaxLength(256);
        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.VerifiedAt);
        builder.Property(e => e.InvalidatedAt);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.ExpiresAt);
    }
}
