namespace Api.Infrastructure.Entities;

/// <summary>
/// Represents a template for LLM prompts.
/// A template can have multiple versions, with one version active at a time.
/// </summary>
public class PromptTemplateEntity
{
    /// <summary>
    /// Unique identifier for the prompt template.
    /// </summary>
    required public string Id { get; set; }

    /// <summary>
    /// Unique name for the template (e.g., "qa-system-prompt", "explain-rules-prompt").
    /// Used as a key for retrieving the active version at runtime.
    /// </summary>
    required public string Name { get; set; }

    /// <summary>
    /// Human-readable description of the template's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category for organizing templates (e.g., "qa", "explain", "setup", "system").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// ID of the user who created this template.
    /// </summary>
    required public string CreatedByUserId { get; set; }

    /// <summary>
    /// Timestamp when the template was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// The user who created this template.
    /// </summary>
    required public UserEntity CreatedBy { get; set; }

    /// <summary>
    /// All versions of this template (ordered by version number).
    /// </summary>
    public ICollection<PromptVersionEntity> Versions { get; set; } = new List<PromptVersionEntity>();

    /// <summary>
    /// Audit log entries for this template.
    /// </summary>
    public ICollection<PromptAuditLogEntity> AuditLogs { get; set; } = new List<PromptAuditLogEntity>();
}
