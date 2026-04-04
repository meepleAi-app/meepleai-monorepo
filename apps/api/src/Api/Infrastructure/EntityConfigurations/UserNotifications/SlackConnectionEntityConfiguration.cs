using Api.Infrastructure.Entities.UserNotifications;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserNotifications;

/// <summary>
/// EF Core configuration for SlackConnectionEntity.
/// Configures table mapping, indexes, and constraints for Slack workspace connections.
/// Encrypts BotAccessToken at rest using IDataProtector.
/// </summary>
internal class SlackConnectionEntityConfiguration : IEntityTypeConfiguration<SlackConnectionEntity>
{
    private readonly IDataProtector? _protector;

    public SlackConnectionEntityConfiguration() { }

    public SlackConnectionEntityConfiguration(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector("MeepleAI.SlackSecrets");
    }

    public void Configure(EntityTypeBuilder<SlackConnectionEntity> builder)
    {
        builder.ToTable("slack_connections");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.SlackUserId).HasColumnName("slack_user_id").HasMaxLength(20).IsRequired();
        builder.Property(e => e.SlackTeamId).HasColumnName("slack_team_id").HasMaxLength(20).IsRequired();
        builder.Property(e => e.SlackTeamName).HasColumnName("slack_team_name").HasMaxLength(100).IsRequired();
        builder.Property(e => e.BotAccessToken).HasColumnName("bot_access_token").IsRequired();

        // Encrypt BotAccessToken at rest using Data Protection
        if (_protector != null)
        {
            builder.Property(e => e.BotAccessToken)
                .HasConversion(
                    v => _protector.Protect(v),
                    v => _protector.Unprotect(v));
        }
        builder.Property(e => e.DmChannelId).HasColumnName("dm_channel_id").HasMaxLength(20).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.ConnectedAt).HasColumnName("connected_at").IsRequired();
        builder.Property(e => e.DisconnectedAt).HasColumnName("disconnected_at");

        // Unique constraint: one active connection per user per workspace
        builder.HasIndex(e => new { e.UserId, e.SlackTeamId })
            .HasDatabaseName("IX_slack_connections_user_id_slack_team_id")
            .IsUnique();

        // Lookup by Slack user ID
        builder.HasIndex(e => e.SlackUserId)
            .HasDatabaseName("IX_slack_connections_slack_user_id");

        // Active connections filter
        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_slack_connections_is_active")
            .HasFilter("is_active = true");
    }
}
