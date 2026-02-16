using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// EF Core configuration for InsightFeedback entity.
/// Issue #4124: AI Insights Runtime Validation (Performance + Accuracy).
/// </summary>
internal sealed class InsightFeedbackEntityConfiguration : IEntityTypeConfiguration<InsightFeedbackEntity>
{
    public void Configure(EntityTypeBuilder<InsightFeedbackEntity> builder)
    {
        builder.ToTable("insight_feedback", "administration");

        builder.HasKey(f => f.Id);

        // Indexes for accuracy queries
        builder.HasIndex(f => f.UserId);
        builder.HasIndex(f => f.InsightType);
        builder.HasIndex(f => f.SubmittedAt);
        builder.HasIndex(f => new { f.InsightType, f.IsRelevant }); // Accuracy per type
        builder.HasIndex(f => new { f.UserId, f.InsightId }).IsUnique(); // One feedback per insight per user
    }
}
