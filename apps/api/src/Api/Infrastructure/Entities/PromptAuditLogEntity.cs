namespace Api.Infrastructure.Entities;

/// <summary>
/// Represents an audit log entry for prompt template changes.
/// Records all create, update, activate, and deactivate operations.
/// </summary>
public class PromptAuditLogEntity
{
    /// <summary>
    /// Unique identifier for the audit log entry.
    /// </summary>
    // DDD-PHASE2: Converted to Guid for domain alignment
    required public Guid Id { get; set; }

    /// <summary>
    /// ID of the template this log entry is for.
    /// </summary>
    // DDD-PHASE2: Converted to Guid for domain alignment
    required public Guid TemplateId { get; set; }

    /// <summary>
    /// ID of the specific version (if applicable).
    /// Null for template-level operations (e.g., template creation).
    /// </summary>
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid? VersionId { get; set; }

    /// <summary>
    /// The action performed (e.g., "template_created", "version_created", "version_activated", "version_deactivated").
    /// </summary>
    required public string Action { get; set; }

    /// <summary>
    /// ID of the user who performed the action.
    /// </summary>
    // DDD-PHASE2: Converted to Guid for domain alignment
    required public Guid ChangedByUserId { get; set; }

    /// <summary>
    /// Timestamp when the action was performed.
    /// </summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>
    /// Additional details about the change (JSON format).
    /// Can include previous values, reason for change, etc.
    /// </summary>
    public string? Details { get; set; }

    // Navigation properties
    /// <summary>
    /// The template this log entry is for.
    /// </summary>
    required public PromptTemplateEntity Template { get; set; }

    /// <summary>
    /// The specific version this log entry is for (if applicable).
    /// </summary>
    public PromptVersionEntity? Version { get; set; }

    /// <summary>
    /// The user who performed the action.
    /// </summary>
    required public UserEntity ChangedBy { get; set; }
}
