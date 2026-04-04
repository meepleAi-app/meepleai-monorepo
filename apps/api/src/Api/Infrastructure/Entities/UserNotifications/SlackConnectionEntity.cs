using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.UserNotifications;

/// <summary>
/// EF Core entity for Slack workspace connections.
/// Persistence model for SlackConnection aggregate.
/// Stores OAuth bot tokens and DM channel mappings per user.
/// </summary>
[Table("slack_connections")]
public class SlackConnectionEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("slack_user_id")]
    public string SlackUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("slack_team_id")]
    public string SlackTeamId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("slack_team_name")]
    public string SlackTeamName { get; set; } = string.Empty;

    [Required]
    [Column("bot_access_token")]
    public string BotAccessToken { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("dm_channel_id")]
    public string DmChannelId { get; set; } = string.Empty;

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("connected_at")]
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    [Column("disconnected_at")]
    public DateTime? DisconnectedAt { get; set; }
}
