using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.KnowledgeBase;

/// <summary>
/// Entity Framework configuration for ChatSession entity.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class ChatSessionEntityConfiguration : IEntityTypeConfiguration<ChatSessionEntity>
{
    public void Configure(EntityTypeBuilder<ChatSessionEntity> builder)
    {
        builder.ToTable("chat_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(s => s.GameId)
            .HasColumnName("game_id")
            .IsRequired();

        builder.Property(s => s.UserLibraryEntryId)
            .HasColumnName("user_library_entry_id");

        builder.Property(s => s.AgentSessionId)
            .HasColumnName("agent_session_id");

        builder.Property(s => s.Title)
            .HasColumnName("title")
            .HasMaxLength(200);

        builder.Property(s => s.AgentConfigJson)
            .HasColumnName("agent_config_json")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.LastMessageAt)
            .HasColumnName("last_message_at")
            .IsRequired();

        builder.Property(s => s.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(s => s.MessagesJson)
            .HasColumnName("messages_json")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("[]");

        // Indexes for query performance
        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("ix_chat_sessions_user_id");

        builder.HasIndex(s => s.GameId)
            .HasDatabaseName("ix_chat_sessions_game_id");

        builder.HasIndex(s => new { s.UserId, s.GameId })
            .HasDatabaseName("ix_chat_sessions_user_game");

        builder.HasIndex(s => s.LastMessageAt)
            .HasDatabaseName("ix_chat_sessions_last_message_at");

        builder.HasIndex(s => s.AgentSessionId)
            .HasDatabaseName("ix_chat_sessions_agent_session_id");

        // Foreign key to User
        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign key to Game
        builder.HasOne(s => s.Game)
            .WithMany()
            .HasForeignKey(s => s.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
