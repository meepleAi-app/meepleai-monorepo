using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure.Entities.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.Authentication;

/// <summary>
/// Entity Framework configuration for ShareLink entity.
/// </summary>
public sealed class ShareLinkEntityConfiguration : IEntityTypeConfiguration<ShareLinkEntity>
{
    public void Configure(EntityTypeBuilder<ShareLinkEntity> builder)
    {
        builder.ToTable("share_links");

        builder.HasKey(sl => sl.Id);

        builder.Property(sl => sl.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(sl => sl.ThreadId)
            .HasColumnName("thread_id")
            .IsRequired();

        builder.Property(sl => sl.CreatorId)
            .HasColumnName("creator_id")
            .IsRequired();

        builder.Property(sl => sl.Role)
            .HasColumnName("role")
            .HasConversion<string>() // Store enum as string
            .IsRequired();

        builder.Property(sl => sl.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(sl => sl.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(sl => sl.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(sl => sl.Label)
            .HasColumnName("label")
            .HasMaxLength(200);

        builder.Property(sl => sl.AccessCount)
            .HasColumnName("access_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(sl => sl.LastAccessedAt)
            .HasColumnName("last_accessed_at");

        // Indexes for query performance
        builder.HasIndex(sl => sl.ThreadId)
            .HasDatabaseName("ix_share_links_thread_id");

        builder.HasIndex(sl => sl.CreatorId)
            .HasDatabaseName("ix_share_links_creator_id");

        builder.HasIndex(sl => sl.ExpiresAt)
            .HasDatabaseName("ix_share_links_expires_at");

        builder.HasIndex(sl => sl.RevokedAt)
            .HasDatabaseName("ix_share_links_revoked_at");

        // Foreign keys
        builder.HasOne(sl => sl.ChatThread)
            .WithMany()
            .HasForeignKey(sl => sl.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sl => sl.Creator)
            .WithMany()
            .HasForeignKey(sl => sl.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
