using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.UserNotifications;

/// <summary>
/// EF Core entity for Slack team channel webhook configurations.
/// Stores incoming webhook URLs and notification type routing for team-wide channels.
/// </summary>
[Table("slack_team_channel_configs")]
public class SlackTeamChannelConfigEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    [Column("channel_name")]
    public string ChannelName { get; set; } = string.Empty;

    [Required]
    [Column("webhook_url")]
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of notification type strings this channel should receive.
    /// Stored as JSONB for efficient querying.
    /// </summary>
    [Required]
    [Column("notification_types", TypeName = "jsonb")]
    public string NotificationTypes { get; set; } = "[]";

    [Required]
    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Required]
    [Column("overrides_default")]
    public bool OverridesDefault { get; set; }
}
