using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Authentication;

/// <summary>
/// EF Core configuration for the append-only TermsAcceptance entity (#2954 F1).
/// Auto-applied via ApplyConfigurationsFromAssembly (mirrors UserAiConsent).
/// </summary>
internal sealed class TermsAcceptanceEntityConfiguration : IEntityTypeConfiguration<TermsAcceptance>
{
    public void Configure(EntityTypeBuilder<TermsAcceptance> builder)
    {
        builder.ToTable("user_terms_acceptances");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.TermsVersion).IsRequired().HasMaxLength(50);
        builder.Property(e => e.AcceptedAt).IsRequired();

        // Persisted as the enum's string name (not int) → no CHECK-range drift
        // when the enum grows (avoids the #2974 constraint pitfall).
        builder.Property(e => e.Context)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(e => e.IpAddress).HasMaxLength(45);
        builder.Property(e => e.UserAgent).HasMaxLength(512);
        builder.Property(e => e.CreatedAt).IsRequired();

        // FK to users with cascade delete (no navigation property needed).
        builder.HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Latest-acceptance lookup. Append-only: intentionally NO unique index on UserId.
        builder.HasIndex(e => new { e.UserId, e.AcceptedAt })
            .HasDatabaseName("ix_user_terms_acceptances_user_accepted");
    }
}
