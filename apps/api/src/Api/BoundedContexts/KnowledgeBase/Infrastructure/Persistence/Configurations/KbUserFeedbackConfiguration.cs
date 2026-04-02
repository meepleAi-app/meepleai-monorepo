using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

internal sealed class KbUserFeedbackConfiguration : IEntityTypeConfiguration<KbUserFeedback>
{
    public void Configure(EntityTypeBuilder<KbUserFeedback> builder)
    {
        builder.ToTable("kb_user_feedback", "knowledge_base");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.GameId).HasColumnName("game_id").IsRequired();
        builder.Property(e => e.ChatSessionId).HasColumnName("chat_session_id").IsRequired();
        builder.Property(e => e.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(e => e.Outcome).HasColumnName("outcome").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Comment).HasColumnName("comment").HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(e => e.GameId).HasDatabaseName("IX_kb_user_feedback_game_id");
        builder.HasIndex(e => e.UserId).HasDatabaseName("IX_kb_user_feedback_user_id");
        builder.HasIndex(e => e.MessageId).HasDatabaseName("IX_kb_user_feedback_message_id");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_kb_user_feedback_created_at");
        builder.Ignore(e => e.DomainEvents);
    }
}
