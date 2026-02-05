using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for SessionNoteEntity.
/// </summary>
public class SessionNoteConfiguration : IEntityTypeConfiguration<SessionNoteEntity>
{
    public void Configure(EntityTypeBuilder<SessionNoteEntity> builder)
    {
        builder.ToTable("SessionNotes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SessionId)
            .IsRequired();

        builder.Property(e => e.ParticipantId)
            .IsRequired();

        builder.Property(e => e.EncryptedContent)
            .IsRequired()
            .HasMaxLength(65536);  // Allow for larger encrypted notes

        builder.Property(e => e.IsRevealed)
            .HasDefaultValue(false);

        builder.Property(e => e.ObscuredText)
            .HasMaxLength(500);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false);

        // Indexes
        builder.HasIndex(e => e.SessionId);
        builder.HasIndex(e => e.ParticipantId);
        builder.HasIndex(e => new { e.SessionId, e.ParticipantId });
        builder.HasIndex(e => e.IsDeleted);

        // Soft delete query filter
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Relationships
        builder.HasOne(e => e.Session)
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
