namespace Api.Infrastructure.Entities;

/// <summary>
/// Represents a specific version of a prompt template.
/// Each template can have multiple versions, but only one can be active at a time.
/// </summary>
public class PromptVersionEntity
{
    /// <summary>
    /// Unique identifier for the version.
    /// </summary>
    required public string Id { get; set; }

    /// <summary>
    /// ID of the parent template.
    /// </summary>
    required public string TemplateId { get; set; }

    /// <summary>
    /// Sequential version number (1, 2, 3, ...).
    /// Unique within a template.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// The actual prompt content.
    /// Can be plain text or structured JSON for complex prompts.
    /// </summary>
    required public string Content { get; set; }

    /// <summary>
    /// Whether this version is currently active.
    /// Only one version per template can be active at a time.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// ID of the user who created this version.
    /// </summary>
    required public string CreatedByUserId { get; set; }

    /// <summary>
    /// Timestamp when this version was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Optional JSON metadata for this version (e.g., model parameters, tags, notes).
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties
    /// <summary>
    /// The parent template.
    /// </summary>
    required public PromptTemplateEntity Template { get; set; }

    /// <summary>
    /// The user who created this version.
    /// </summary>
    required public UserEntity CreatedBy { get; set; }

    /// <summary>
    /// Audit log entries for this version.
    /// </summary>
    public ICollection<PromptAuditLogEntity> AuditLogs { get; set; } = new List<PromptAuditLogEntity>();
}
