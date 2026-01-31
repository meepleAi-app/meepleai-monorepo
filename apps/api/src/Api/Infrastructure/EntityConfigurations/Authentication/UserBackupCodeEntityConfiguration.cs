using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

// AUTH-07: User backup codes for 2FA recovery
internal class UserBackupCodeEntityConfiguration : IEntityTypeConfiguration<UserBackupCodeEntity>
{
    public void Configure(EntityTypeBuilder<UserBackupCodeEntity> builder)
    {
        builder.ToTable("user_backup_codes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.CodeHash).IsRequired().HasMaxLength(255);
        builder.Property(e => e.IsUsed).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.UsedAt);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => new { e.UserId, e.IsUsed }).HasFilter("\"IsUsed\" = FALSE");
        builder.HasIndex(e => e.CodeHash).IsUnique();
    }
}
