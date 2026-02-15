using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ArbitroValidationFeedback entity.
/// Issue #4328: Arbitro Agent Beta Testing and User Feedback Iteration.
/// </summary>
public sealed class ArbitroValidationFeedbackConfiguration : IEntityTypeConfiguration<ArbitroValidationFeedbackEntity>
{
    public void Configure(EntityTypeBuilder<ArbitroValidationFeedbackEntity> builder)
    {
        builder.ToTable("arbitro_validation_feedback", "knowledge_base");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(f => f.ValidationId).HasColumnName("validation_id").IsRequired();
        builder.Property(f => f.GameSessionId).HasColumnName("game_session_id").IsRequired();
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(f => f.Rating)
            .HasColumnName("rating")
            .IsRequired();

        builder.Property(f => f.Accuracy)
            .HasColumnName("accuracy")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.Comment)
            .HasColumnName("comment")
            .HasMaxLength(2000);

        builder.Property(f => f.AiDecision)
            .HasColumnName("ai_decision")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.AiConfidence)
            .HasColumnName("ai_confidence")
            .IsRequired();

        builder.Property(f => f.HadConflicts)
            .HasColumnName("had_conflicts")
            .IsRequired();

        builder.Property(f => f.SubmittedAt)
            .HasColumnName("submitted_at")
            .IsRequired();

        // Indexes for beta testing queries and metrics aggregation
        builder.HasIndex(f => f.ValidationId)
            .IsUnique()
            .HasDatabaseName("ix_arbitro_validation_feedback_validation_id");

        builder.HasIndex(f => f.GameSessionId)
            .HasDatabaseName("ix_arbitro_validation_feedback_game_session_id");

        builder.HasIndex(f => f.UserId)
            .HasDatabaseName("ix_arbitro_validation_feedback_user_id");

        builder.HasIndex(f => f.SubmittedAt)
            .HasDatabaseName("ix_arbitro_validation_feedback_submitted_at");

        builder.HasIndex(f => f.HadConflicts)
            .HasDatabaseName("ix_arbitro_validation_feedback_had_conflicts");

        builder.HasIndex(f => new { f.Accuracy, f.SubmittedAt })
            .HasDatabaseName("ix_arbitro_validation_feedback_accuracy_submitted_at");

        // Navigation properties (optional, depends on DbContext setup)
        // builder.HasOne(f => f.GameSession).WithMany().HasForeignKey(f => f.GameSessionId);
        // builder.HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId);
    }
}
