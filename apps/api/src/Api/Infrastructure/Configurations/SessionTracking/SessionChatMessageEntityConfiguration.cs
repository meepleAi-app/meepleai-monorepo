using Api.Infrastructure.Entities.SessionTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SessionTracking;

/// <summary>
/// EF Core configuration for SessionChatMessageEntity.
/// Issue #4760: SessionMedia Entity + RAG Agent Integration + Shared Chat
/// </summary>
internal sealed class SessionChatMessageEntityConfiguration : IEntityTypeConfiguration<SessionChatMessageEntity>
{
    public void Configure(EntityTypeBuilder<SessionChatMessageEntity> builder)
    {
        builder.ToTable("session_tracking_chat_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(m => m.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(m => m.SenderId)
            .HasColumnName("sender_id");

        builder.Property(m => m.Content)
            .HasColumnName("content")
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(m => m.MessageType)
            .HasColumnName("message_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.TurnNumber)
            .HasColumnName("turn_number");

        builder.Property(m => m.SequenceNumber)
            .HasColumnName("sequence_number")
            .IsRequired();

        builder.Property(m => m.AgentType)
            .HasColumnName("agent_type")
            .HasMaxLength(50);

        builder.Property(m => m.Confidence)
            .HasColumnName("confidence");

        builder.Property(m => m.CitationsJson)
            .HasColumnName("citations_json");

        builder.Property(m => m.MentionsJson)
            .HasColumnName("mentions_json");

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(m => m.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(m => m.DeletedAt)
            .HasColumnName("deleted_at");

        // Global query filter for soft-delete
        builder.HasQueryFilter(m => !m.IsDeleted);

        // Indexes
        builder.HasIndex(m => m.SessionId)
            .HasDatabaseName("ix_session_tracking_chat_messages_session_id");

        builder.HasIndex(m => new { m.SessionId, m.SequenceNumber })
            .HasDatabaseName("ix_session_tracking_chat_messages_session_id_sequence_number")
            .IsUnique();

        builder.HasIndex(m => m.SenderId)
            .HasDatabaseName("ix_session_tracking_chat_messages_sender_id");

        // Navigation properties
        builder.HasOne(m => m.Session)
            .WithMany()
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
