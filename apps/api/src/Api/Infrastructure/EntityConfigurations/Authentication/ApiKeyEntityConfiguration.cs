using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

public class ApiKeyEntityConfiguration : IEntityTypeConfiguration<ApiKeyEntity>
{
    public void Configure(EntityTypeBuilder<ApiKeyEntity> builder)
    {
        builder.ToTable("api_keys");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.KeyName).IsRequired().HasMaxLength(128);
        builder.Property(e => e.KeyHash).IsRequired().HasMaxLength(256);
        builder.Property(e => e.KeyPrefix).IsRequired().HasMaxLength(16);
        builder.Property(e => e.Scopes).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.LastUsedAt);
        builder.Property(e => e.ExpiresAt);
        builder.Property(e => e.RevokedAt);
        builder.Property(e => e.RevokedBy).HasMaxLength(64);
        builder.Property(e => e.Metadata).HasMaxLength(4096);
        builder.Property(e => e.UsageCount).IsRequired().HasDefaultValue(0);
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.RevokedByUser)
            .WithMany()
            .HasForeignKey(e => e.RevokedBy)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => e.KeyHash).IsUnique();
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => new { e.IsActive, e.ExpiresAt });
    }
}
