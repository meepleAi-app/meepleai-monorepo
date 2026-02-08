using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// EF Core configuration for UserTokenUsage entity (Issue #3786)
/// </summary>
internal class UserTokenUsageEntityConfiguration : IEntityTypeConfiguration<UserTokenUsage>
{
    public void Configure(EntityTypeBuilder<UserTokenUsage> builder)
    {
        builder.ToTable("user_token_usage");

        // Primary Key
        builder.HasKey(e => e.Id);

        // Foreign Keys
        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.TierId)
            .IsRequired();

        // Usage tracking
        builder.Property(e => e.TokensUsed)
            .IsRequired();

        builder.Property(e => e.MessagesCount)
            .IsRequired();

        builder.Property(e => e.Cost)
            .IsRequired()
            .HasPrecision(18, 4); // More precision for cost tracking

        builder.Property(e => e.LastReset)
            .IsRequired();

        // Status flags
        builder.Property(e => e.IsBlocked)
            .IsRequired();

        builder.Property(e => e.IsNearLimit)
            .IsRequired();

        // Complex type: Warnings (JSON array of timestamps)
        builder.Property(e => e.Warnings)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<DateTime>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<DateTime>())
            .HasColumnType("jsonb");

        // Complex type: History (JSON array of monthly snapshots)
        builder.Property(e => e.History)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<MonthlyUsageSnapshot>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<MonthlyUsageSnapshot>())
            .HasColumnType("jsonb");

        // Audit
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        // Indexes for performance
        builder.HasIndex(e => e.UserId)
            .IsUnique(); // One usage record per user

        builder.HasIndex(e => new { e.UserId, e.TokensUsed })
            .HasDatabaseName("IX_UserTokenUsage_UserId_TokensUsed");

        builder.HasIndex(e => new { e.TierId, e.IsBlocked })
            .HasDatabaseName("IX_UserTokenUsage_TierId_IsBlocked");

        builder.HasIndex(e => e.LastReset);
    }
}
