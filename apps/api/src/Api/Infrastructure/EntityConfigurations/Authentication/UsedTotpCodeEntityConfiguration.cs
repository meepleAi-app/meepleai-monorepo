using Api.Infrastructure.Entities.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Authentication;

/// <summary>
/// EF Core configuration for UsedTotpCodeEntity.
/// SECURITY: Issue #1787 - TOTP Replay Attack Prevention (OWASP ASVS 2.8.3)
/// </summary>
public class UsedTotpCodeEntityConfiguration : IEntityTypeConfiguration<UsedTotpCodeEntity>
{
    public void Configure(EntityTypeBuilder<UsedTotpCodeEntity> builder)
    {
        builder.ToTable("used_totp_codes");

        // Primary Key
        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.Id)
            .IsRequired();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.CodeHash)
            .IsRequired()
            .HasMaxLength(128); // SHA256 hash length (64 hex chars)

        builder.Property(e => e.TimeStep)
            .IsRequired();

        builder.Property(e => e.UsedAt)
            .IsRequired();

        builder.Property(e => e.ExpiresAt)
            .IsRequired();

        // Indexes
        // SECURITY: UNIQUE constraint prevents TOTP replay attacks at DB level
        // Fixes race condition where concurrent requests could both pass AnyAsync() check
        // Order: (UserId, CodeHash) ensures atomicity - duplicate insert will fail
        builder.HasIndex(e => new { e.UserId, e.CodeHash })
            .IsUnique()
            .HasDatabaseName("ix_used_totp_codes_user_code_unique");

        // PERFORMANCE: Index for background cleanup job (non-unique)
        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_used_totp_codes_expiry");

        // Foreign Key Relationship
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade); // Delete used codes when user is deleted
    }
}