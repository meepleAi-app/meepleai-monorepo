using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class AiRequestLogEntityConfiguration : IEntityTypeConfiguration<AiRequestLogEntity>
{
    public void Configure(EntityTypeBuilder<AiRequestLogEntity> builder)
    {
        builder.ToTable("ai_request_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.UserId).HasMaxLength(64);
        builder.Property(e => e.GameId).HasMaxLength(64);
        builder.Property(e => e.Endpoint).IsRequired().HasMaxLength(32);
        builder.Property(e => e.Query).HasMaxLength(2048);
        builder.Property(e => e.ResponseSnippet).HasMaxLength(1024);
        builder.Property(e => e.LatencyMs).IsRequired();
        builder.Property(e => e.TokenCount).HasDefaultValue(0);
        builder.Property(e => e.PromptTokens).HasDefaultValue(0);
        builder.Property(e => e.CompletionTokens).HasDefaultValue(0);
        builder.Property(e => e.Confidence);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(32);
        builder.Property(e => e.ErrorMessage).HasMaxLength(1024);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.UserAgent).HasMaxLength(256);
        builder.Property(e => e.Model).HasMaxLength(128);
        builder.Property(e => e.FinishReason).HasMaxLength(64);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.Endpoint);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.GameId);
    }
}
