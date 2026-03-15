using Api.Infrastructure.Entities.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class AccessRequestEntityConfiguration : IEntityTypeConfiguration<AccessRequestEntity>
{
    public void Configure(EntityTypeBuilder<AccessRequestEntity> builder)
    {
        builder.ToTable("access_requests");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Email).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(20);
        builder.Property(e => e.RequestedAt).IsRequired();
        builder.Property(e => e.ReviewedAt);
        builder.Property(e => e.ReviewedBy);
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
        builder.Property(e => e.InvitationId);

        // Unique index: only one pending request per email
        builder.HasIndex(e => new { e.Email, e.Status })
            .HasFilter("\"status\" = 'Pending'")
            .IsUnique();

        builder.HasIndex(e => e.RequestedAt);

        // FK relationships
        builder.HasOne(e => e.ReviewedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReviewedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Invitation)
            .WithMany()
            .HasForeignKey(e => e.InvitationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
