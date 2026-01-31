using System;

namespace Api.Infrastructure.Entities.Administration;

/// <summary>
/// Alert configuration entity for dynamic multi-channel settings (Issue #921)
/// Stores configuration for Email, Slack, PagerDuty, and global settings.
/// </summary>
public class AlertConfigurationEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Configuration key (e.g., "Email.SmtpHost", "Slack.WebhookUrl")
    /// Unique constraint ensures no duplicate keys.
    /// </summary>
    public required string ConfigKey { get; set; }

    /// <summary>
    /// Configuration value (JSON for complex types, string for simple)
    /// Encrypted if IsEncrypted is true.
    /// </summary>
    public required string ConfigValue { get; set; }

    /// <summary>
    /// Category: Email, Slack, PagerDuty, Global
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Whether ConfigValue is encrypted (for passwords, API keys)
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Human-readable description of this config setting
    /// </summary>
    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; }
    public required string UpdatedBy { get; set; }
}
