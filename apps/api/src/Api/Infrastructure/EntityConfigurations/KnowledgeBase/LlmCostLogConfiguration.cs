using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for LlmCostLogEntity
/// ISSUE-960: BGAI-018 - Cost tracking database schema
/// </summary>
public class LlmCostLogConfiguration : IEntityTypeConfiguration<LlmCostLogEntity>
{
    public void Configure(EntityTypeBuilder<LlmCostLogEntity> builder)
    {
        builder.ToTable("llm_cost_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.UserRole)
            .HasColumnName("user_role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ModelId)
            .HasColumnName("model_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.PromptTokens)
            .HasColumnName("prompt_tokens")
            .IsRequired();

        builder.Property(x => x.CompletionTokens)
            .HasColumnName("completion_tokens")
            .IsRequired();

        builder.Property(x => x.TotalTokens)
            .HasColumnName("total_tokens")
            .IsRequired();

        // Decimal precision: 18 total digits, 6 decimal places (micro-dollar precision)
        builder.Property(x => x.InputCost)
            .HasColumnName("input_cost_usd")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(x => x.OutputCost)
            .HasColumnName("output_cost_usd")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(x => x.TotalCost)
            .HasColumnName("total_cost_usd")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(x => x.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(500);

        builder.Property(x => x.LatencyMs)
            .HasColumnName("latency_ms")
            .IsRequired();

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45); // IPv6 max length

        builder.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.RequestDate)
            .HasColumnName("request_date")
            .IsRequired();

        // Indexes for efficient querying
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_llm_cost_logs_user_id");

        builder.HasIndex(x => x.RequestDate)
            .HasDatabaseName("ix_llm_cost_logs_request_date");

        builder.HasIndex(x => new { x.Provider, x.RequestDate })
            .HasDatabaseName("ix_llm_cost_logs_provider_date");

        builder.HasIndex(x => new { x.UserRole, x.RequestDate })
            .HasDatabaseName("ix_llm_cost_logs_role_date");

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("ix_llm_cost_logs_created_at");

        // Foreign key relationship to Users
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
