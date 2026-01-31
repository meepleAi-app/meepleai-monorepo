using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

// AUTH-06: OAuth Accounts Configuration
internal class OAuthAccountEntityConfiguration : IEntityTypeConfiguration<OAuthAccountEntity>
{
    public void Configure(EntityTypeBuilder<OAuthAccountEntity> builder)
    {
        builder.ToTable("oauth_accounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Provider).IsRequired().HasMaxLength(20);
        builder.Property(e => e.ProviderUserId).IsRequired().HasMaxLength(255);
        builder.Property(e => e.AccessTokenEncrypted).IsRequired();
        builder.Property(e => e.RefreshTokenEncrypted);
        builder.Property(e => e.TokenExpiresAt);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasOne(e => e.User)
            .WithMany(u => u.OAuthAccounts)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.Provider);
        builder.HasIndex(e => new { e.Provider, e.ProviderUserId }).IsUnique();
    }
}
