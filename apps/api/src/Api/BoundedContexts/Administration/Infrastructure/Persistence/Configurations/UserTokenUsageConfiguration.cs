using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for UserTokenUsage entity (Issue #3692)
/// </summary>
public sealed class UserTokenUsageConfiguration : IEntityTypeConfiguration<UserTokenUsage>
{
    public void Configure(EntityTypeBuilder<UserTokenUsage> builder)
    {
        builder.ToTable("user_token_usage", "administration");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(u => u.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(u => u.TierId).HasColumnName("tier_id").IsRequired();

        builder.HasIndex(u => u.UserId).IsUnique();
        builder.HasIndex(u => u.TierId);

        // Current month tracking
        builder.Property(u => u.TokensUsed).HasColumnName("tokens_used").IsRequired();
        builder.Property(u => u.MessagesCount).HasColumnName("messages_count").IsRequired();
        builder.Property(u => u.Cost).HasColumnName("cost").HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(u => u.LastReset).HasColumnName("last_reset").IsRequired();

        // Status flags
        builder.Property(u => u.IsBlocked).HasColumnName("is_blocked").IsRequired();
        builder.Property(u => u.IsNearLimit).HasColumnName("is_near_limit").IsRequired();

        // Warnings as JSON array
        builder.Property(u => u.Warnings)
            .HasColumnName("warnings")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<DateTime>>(v, (JsonSerializerOptions)null!) ?? new List<DateTime>()
            );

        // History as JSON array of MonthlyUsageSnapshot
        builder.Property(u => u.History)
            .HasColumnName("history")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<MonthlyUsageSnapshot>>(v, (JsonSerializerOptions)null!) ?? new List<MonthlyUsageSnapshot>()
            );

        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
