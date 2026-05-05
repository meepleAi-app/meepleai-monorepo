using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicClaimEntity"/>.
/// </summary>
internal sealed class MechanicClaimEntityConfiguration : IEntityTypeConfiguration<MechanicClaimEntity>
{
    public void Configure(EntityTypeBuilder<MechanicClaimEntity> builder)
    {
        builder.ToTable("mechanic_claims", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_claims_status_range",
                "status BETWEEN 0 AND 2");

            t.HasCheckConstraint(
                "ck_mechanic_claims_section_range",
                "section BETWEEN 0 AND 5");

            t.HasCheckConstraint(
                "ck_mechanic_claims_display_order_non_negative",
                "display_order >= 0");

            // A rejected claim must have a rejection note; an approved claim has reviewer+timestamp.
            t.HasCheckConstraint(
                "ck_mechanic_claims_rejection_note_when_rejected",
                "status <> 2 OR rejection_note IS NOT NULL");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").IsRequired();
        builder.Property(c => c.AnalysisId).HasColumnName("analysis_id").IsRequired();
        builder.Property(c => c.Section).HasColumnName("section").IsRequired();

        builder.Property(c => c.Text)
            .HasColumnName("text")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasDefaultValue(0).IsRequired();
        builder.Property(c => c.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(c => c.ReviewedAt).HasColumnName("reviewed_at");

        builder.Property(c => c.RejectionNote)
            .HasColumnName("rejection_note")
            .HasMaxLength(2000);

        builder.HasIndex(c => c.AnalysisId).HasDatabaseName("ix_mechanic_claims_analysis_id");
        builder.HasIndex(c => new { c.AnalysisId, c.Section, c.DisplayOrder })
            .HasDatabaseName("ix_mechanic_claims_analysis_section_order");
        builder.HasIndex(c => c.Status).HasDatabaseName("ix_mechanic_claims_status");

        builder.HasMany(c => c.Citations)
            .WithOne(ci => ci.Claim)
            .HasForeignKey(ci => ci.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
