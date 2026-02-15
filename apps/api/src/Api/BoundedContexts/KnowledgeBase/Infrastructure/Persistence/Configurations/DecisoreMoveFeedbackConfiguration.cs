using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for DecisoreMoveFeedback entity.
/// Issue #4335: Decisore Agent Beta Testing and User Feedback Iteration.
/// </summary>
public sealed class DecisoreMoveFeedbackConfiguration : IEntityTypeConfiguration<DecisoreMoveFeedbackEntity>
{
    public void Configure(EntityTypeBuilder<DecisoreMoveFeedbackEntity> builder)
    {
        builder.ToTable("decisore_move_feedback", "knowledge_base");

        builder.HasKey(f => f.Id);

        // Indexes for beta testing queries
        builder.HasIndex(f => f.SuggestionId).IsUnique();
        builder.HasIndex(f => f.GameSessionId);
        builder.HasIndex(f => f.UserId);
        builder.HasIndex(f => f.SubmittedAt);
        builder.HasIndex(f => f.SuggestionFollowed);
        builder.HasIndex(f => f.AnalysisDepth);
        builder.HasIndex(f => new { f.Outcome, f.SuggestionFollowed }); // Win correlation queries
        builder.HasIndex(f => new { f.Quality, f.SubmittedAt }); // Quality trend analysis
    }
}
