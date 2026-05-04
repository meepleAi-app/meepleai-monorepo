using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.DocumentProcessing;

/// <summary>
/// EF Core entity configuration for <see cref="PhotoBatchUpload"/> and its
/// owned child entity <see cref="PhotoBatchPage"/>.
/// Direct domain-entity mapping — no separate persistence entity layer.
/// Libro Game AI Assistant MVP Phase 1.
/// </summary>
internal sealed class PhotoBatchUploadEntityConfiguration
    : IEntityTypeConfiguration<PhotoBatchUpload>
{
    public void Configure(EntityTypeBuilder<PhotoBatchUpload> builder)
    {
        builder.ToTable("photo_batch_uploads");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.UserId)
            .IsRequired();

        builder.Property(b => b.GameId)
            .IsRequired();

        builder.Property(b => b.SourceLanguage)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.TotalPages)
            .IsRequired();

        builder.Property(b => b.IndexedPages)
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.CompletedAt)
            .IsRequired(false);

        builder.Property(b => b.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(b => b.DeletedAt)
            .IsRequired(false);

        // Optimistic concurrency token
        builder.Property(b => b.RowVersion)
            .IsRowVersion();

        // Soft-delete query filter
        builder.HasQueryFilter(b => !b.IsDeleted);

        // Indices
        builder.HasIndex(b => b.UserId)
            .HasDatabaseName("ix_photo_batch_uploads_user_id");

        builder.HasIndex(b => new { b.GameId, b.Status })
            .HasDatabaseName("ix_photo_batch_uploads_game_id_status");

        // Ignore DomainEvents — it is a transient collection managed by AggregateRoot<T>,
        // not a column that should be persisted.
        builder.Ignore(b => b.DomainEvents);

        // FK to users table (restrict delete so user deletion is explicit)
        builder.HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-many: PhotoBatchUpload has many PhotoBatchPages
        builder.HasMany(b => b.Pages)
            .WithOne()
            .HasForeignKey(p => p.PhotoBatchUploadId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tell EF Core to use the private backing field _pages for the navigation
        builder.Navigation(b => b.Pages)
            .HasField("_pages")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>
/// EF Core entity configuration for <see cref="PhotoBatchPage"/>.
/// </summary>
internal sealed class PhotoBatchPageEntityConfiguration
    : IEntityTypeConfiguration<PhotoBatchPage>
{
    public void Configure(EntityTypeBuilder<PhotoBatchPage> builder)
    {
        builder.ToTable("photo_batch_pages");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PhotoBatchUploadId)
            .IsRequired();

        builder.Property(p => p.PageNumber)
            .IsRequired();

        builder.Property(p => p.BlobKey)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.Confidence)
            .IsRequired();

        builder.Property(p => p.ConfidenceLevel)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.Orientation)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.IsBlank)
            .IsRequired();

        // string[] stored as PostgreSQL native text array
        builder.Property(p => p.Warnings)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(p => p.ExtractedText)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(p => p.IndexedAt)
            .IsRequired();

        // Indices
        builder.HasIndex(p => p.PhotoBatchUploadId)
            .HasDatabaseName("ix_photo_batch_pages_batch_id");

        builder.HasIndex(p => new { p.PhotoBatchUploadId, p.PageNumber })
            .IsUnique()
            .HasDatabaseName("uq_photo_batch_pages_batch_page");
    }
}
