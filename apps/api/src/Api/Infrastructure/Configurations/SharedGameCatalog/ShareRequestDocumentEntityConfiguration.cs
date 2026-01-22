using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// Entity Framework configuration for ShareRequestDocument entity.
/// Issue #2724: CreateShareRequest Command infrastructure.
/// </summary>
internal sealed class ShareRequestDocumentEntityConfiguration : IEntityTypeConfiguration<ShareRequestDocumentEntity>
{
    public void Configure(EntityTypeBuilder<ShareRequestDocumentEntity> builder)
    {
        builder.ToTable("share_request_documents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.ShareRequestId)
            .HasColumnName("share_request_id")
            .IsRequired();

        builder.Property(e => e.DocumentId)
            .HasColumnName("document_id")
            .IsRequired();

        builder.Property(e => e.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.FileSize)
            .HasColumnName("file_size")
            .IsRequired();

        builder.Property(e => e.AttachedAt)
            .HasColumnName("attached_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.ShareRequestId)
            .HasDatabaseName("ix_share_request_documents_share_request_id");

        builder.HasIndex(e => e.DocumentId)
            .HasDatabaseName("ix_share_request_documents_document_id");

        builder.HasIndex(e => new { e.ShareRequestId, e.DocumentId })
            .HasDatabaseName("ix_share_request_documents_request_document")
            .IsUnique();
    }
}
