using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for ConversationMemoryEntity.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
internal class ConversationMemoryEntityConfiguration : IEntityTypeConfiguration<ConversationMemoryEntity>
{
    public void Configure(EntityTypeBuilder<ConversationMemoryEntity> builder)
    {
        builder.ToTable("conversation_memory");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.SessionId)
            .IsRequired();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.Content)
            .IsRequired();

        builder.Property(e => e.MessageType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Timestamp)
            .IsRequired();

        // Issue #3547: Vector embedding for semantic search
        builder.Property(e => e.Embedding)
            .HasColumnType("vector(1536)");

        // Indexes for query performance
        builder.HasIndex(e => e.SessionId);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => new { e.UserId, e.GameId });
        builder.HasIndex(e => e.Timestamp);

        // Foreign key to User
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
