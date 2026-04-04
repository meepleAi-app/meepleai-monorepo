using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for LlmRequestLogEntity.
/// Issue #5072: request_logs table with analytics indexes and 30-day retention.
/// </summary>
internal class LlmRequestLogConfiguration : IEntityTypeConfiguration<LlmRequestLogEntity>
{
    public void Configure(EntityTypeBuilder<LlmRequestLogEntity> builder)
    {
        builder.ToTable("llm_request_logs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(x => x.ModelId).HasColumnName("model_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        builder.Property(x => x.RequestSource).HasColumnName("request_source").HasMaxLength(50).HasDefaultValue("Manual").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.UserRole).HasColumnName("user_role").HasMaxLength(50);
        builder.Property(x => x.PromptTokens).HasColumnName("prompt_tokens").IsRequired();
        builder.Property(x => x.CompletionTokens).HasColumnName("completion_tokens").IsRequired();
        builder.Property(x => x.TotalTokens).HasColumnName("total_tokens").IsRequired();
        builder.Property(x => x.CostUsd).HasColumnName("cost_usd").HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(x => x.LatencyMs).HasColumnName("latency_ms").IsRequired();
        builder.Property(x => x.Success).HasColumnName("success").IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(500);
        builder.Property(x => x.IsStreaming).HasColumnName("is_streaming").IsRequired();
        builder.Property(x => x.IsFreeModel).HasColumnName("is_free_model").IsRequired();
        builder.Property(x => x.SessionId).HasColumnName("session_id").HasMaxLength(100);
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();

        // Issue #5511: GDPR pseudonymization tracking
        builder.Property(x => x.IsAnonymized).HasColumnName("is_anonymized").HasDefaultValue(false).IsRequired();

        // Issue #27: Geographic region hint for future multi-region analytics
        builder.Property(x => x.UserRegion).HasColumnName("user_region").HasMaxLength(10);

        // Analytics indexes
        builder.HasIndex(x => new { x.RequestedAt, x.RequestSource })
            .HasDatabaseName("ix_llm_request_logs_requested_at_source");
        builder.HasIndex(x => new { x.UserId, x.RequestedAt })
            .HasDatabaseName("ix_llm_request_logs_user_requested_at");
        builder.HasIndex(x => new { x.ModelId, x.RequestedAt })
            .HasDatabaseName("ix_llm_request_logs_model_requested_at");
        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("ix_llm_request_logs_expires_at");
        builder.HasIndex(x => x.Provider)
            .HasDatabaseName("ix_llm_request_logs_provider");
    }
}
