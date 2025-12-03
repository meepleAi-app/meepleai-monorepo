using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for ChunkedUploadSessionEntity.
/// </summary>
public class ChunkedUploadSessionEntityConfiguration : IEntityTypeConfiguration<ChunkedUploadSessionEntity>
{
    public void Configure(EntityTypeBuilder<ChunkedUploadSessionEntity> builder)
    {
        builder.ToTable("chunked_upload_sessions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.GameId)
            .IsRequired();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.TotalFileSize)
            .IsRequired();

        builder.Property(e => e.TotalChunks)
            .IsRequired();

        builder.Property(e => e.ReceivedChunks)
            .IsRequired();

        builder.Property(e => e.TempDirectory)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.ExpiresAt)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(1024);

        builder.Property(e => e.ReceivedChunkIndices)
            .IsRequired()
            .HasMaxLength(4096); // Support up to ~400 chunks (4GB file)

        // Note: Optimistic concurrency is handled at the application level via
        // UploadChunkCommandHandler retry logic. Shadow property xmin approach
        // doesn't work with the repository pattern (MapToPersistence creates new entities).

        // Foreign keys
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common queries
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ExpiresAt);
        builder.HasIndex(e => new { e.UserId, e.Status });
    }
}
