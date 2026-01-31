using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for ChatThreadCollectionEntity junction table.
/// Issue #2051: Many-to-many relationship between chat threads and document collections
/// </summary>
internal class ChatThreadCollectionEntityConfiguration : IEntityTypeConfiguration<ChatThreadCollectionEntity>
{
    public void Configure(EntityTypeBuilder<ChatThreadCollectionEntity> builder)
    {
        builder.ToTable("chat_thread_collections");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.ChatThreadId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.CollectionId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.CreatedAt).IsRequired();

        // Foreign keys
        builder.HasOne(e => e.ChatThread)
            .WithMany()
            .HasForeignKey(e => e.ChatThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Collection)
            .WithMany()
            .HasForeignKey(e => e.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: one collection per chat thread (can be relaxed later if needed)
        builder.HasIndex(e => new { e.ChatThreadId, e.CollectionId }).IsUnique();
        builder.HasIndex(e => e.ChatThreadId);
    }
}
