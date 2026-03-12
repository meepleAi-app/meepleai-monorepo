using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SessionTracking;

internal class SessionCheckpointEntityConfiguration : IEntityTypeConfiguration<SessionCheckpointEntity>
{
    public void Configure(EntityTypeBuilder<SessionCheckpointEntity> builder)
    {
        builder.ToTable("session_checkpoints");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Timestamp)
            .IsRequired();

        builder.Property(e => e.SnapshotData)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.HasOne(e => e.Session)
            .WithMany()
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SessionId, e.Timestamp });
        builder.HasIndex(e => e.CreatedBy);
    }
}
