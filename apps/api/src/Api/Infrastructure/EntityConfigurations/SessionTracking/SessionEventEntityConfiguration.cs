using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SessionTracking;

internal class SessionEventEntityConfiguration : IEntityTypeConfiguration<SessionEventEntity>
{
    public void Configure(EntityTypeBuilder<SessionEventEntity> builder)
    {
        builder.ToTable("session_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Timestamp)
            .IsRequired();

        builder.Property(e => e.Payload)
            .HasColumnType("jsonb");

        builder.Property(e => e.Source)
            .HasMaxLength(50);

        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.HasOne(e => e.Session)
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SessionId, e.Timestamp });
        builder.HasIndex(e => new { e.SessionId, e.EventType });
        builder.HasIndex(e => e.CreatedBy);
    }
}
