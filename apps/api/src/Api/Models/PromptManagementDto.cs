namespace Api.Models;

/// <summary>
/// Data transfer object for prompt template information.
/// </summary>
public class PromptTemplateDto
{
    /// <summary>
    /// Unique identifier for the template.
    /// </summary>
    required public string Id { get; init; }

    /// <summary>
    /// Unique name for the template.
    /// </summary>
    required public string Name { get; init; }

    /// <summary>
    /// Description of the template's purpose.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Category for organizing templates.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// ID of the user who created this template.
    /// </summary>
    required public string CreatedByUserId { get; init; }

    /// <summary>
    /// Email of the user who created this template.
    /// </summary>
    public string? CreatedByEmail { get; init; }

    /// <summary>
    /// Timestamp when the template was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Number of versions for this template.
    /// </summary>
    public int VersionCount { get; init; }

    /// <summary>
    /// The currently active version number (null if no active version).
    /// </summary>
    public int? ActiveVersionNumber { get; init; }
}

/// <summary>
/// Data transfer object for prompt version information.
/// </summary>
public class PromptVersionDto
{
    /// <summary>
    /// Unique identifier for the version.
    /// </summary>
    required public string Id { get; init; }

    /// <summary>
    /// ID of the parent template.
    /// </summary>
    required public string TemplateId { get; init; }

    /// <summary>
    /// Sequential version number.
    /// </summary>
    public int VersionNumber { get; init; }

    /// <summary>
    /// The actual prompt content.
    /// </summary>
    required public string Content { get; init; }

    /// <summary>
    /// Whether this version is currently active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// ID of the user who created this version.
    /// </summary>
    required public string CreatedByUserId { get; init; }

    /// <summary>
    /// Email of the user who created this version.
    /// </summary>
    public string? CreatedByEmail { get; init; }

    /// <summary>
    /// Timestamp when this version was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Optional metadata for this version.
    /// </summary>
    public string? Metadata { get; init; }
}

/// <summary>
/// Data transfer object for prompt audit log entries.
/// </summary>
public class PromptAuditLogDto
{
    /// <summary>
    /// Unique identifier for the audit log entry.
    /// </summary>
    required public string Id { get; init; }

    /// <summary>
    /// ID of the template this log entry is for.
    /// </summary>
    required public string TemplateId { get; init; }

    /// <summary>
    /// Name of the template.
    /// </summary>
    public string? TemplateName { get; init; }

    /// <summary>
    /// ID of the specific version (if applicable).
    /// </summary>
    public string? VersionId { get; init; }

    /// <summary>
    /// Version number (if applicable).
    /// </summary>
    public int? VersionNumber { get; init; }

    /// <summary>
    /// The action performed.
    /// </summary>
    required public string Action { get; init; }

    /// <summary>
    /// ID of the user who performed the action.
    /// </summary>
    required public string ChangedByUserId { get; init; }

    /// <summary>
    /// Email of the user who performed the action.
    /// </summary>
    public string? ChangedByEmail { get; init; }

    /// <summary>
    /// Timestamp when the action was performed.
    /// </summary>
    public DateTime ChangedAt { get; init; }

    /// <summary>
    /// Additional details about the change.
    /// </summary>
    public string? Details { get; init; }
}

/// <summary>
/// Request to create a new prompt template.
/// </summary>
public class CreatePromptTemplateRequest
{
    /// <summary>
    /// Unique name for the template (e.g., "qa-system-prompt").
    /// </summary>
    required public string Name { get; init; }

    /// <summary>
    /// Description of the template's purpose.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Category for organizing templates (e.g., "qa", "explain", "setup").
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Initial prompt content for version 1.
    /// </summary>
    required public string InitialContent { get; init; }

    /// <summary>
    /// Optional metadata for the initial version.
    /// </summary>
    public string? Metadata { get; init; }
}

/// <summary>
/// Response containing a newly created prompt template.
/// </summary>
public class CreatePromptTemplateResponse
{
    /// <summary>
    /// The newly created template.
    /// </summary>
    required public PromptTemplateDto Template { get; init; }

    /// <summary>
    /// The initial version (version 1).
    /// </summary>
    required public PromptVersionDto InitialVersion { get; init; }
}

/// <summary>
/// Request to create a new version of a prompt template.
/// </summary>
public class CreatePromptVersionRequest
{
    /// <summary>
    /// The prompt content for the new version.
    /// </summary>
    required public string Content { get; init; }

    /// <summary>
    /// Optional metadata for this version.
    /// </summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// Whether to immediately activate this version (default: false).
    /// If false, the version is created but not activated.
    /// </summary>
    public bool ActivateImmediately { get; init; } = false;
}

/// <summary>
/// Request to activate a specific version of a prompt template.
/// </summary>
public class ActivatePromptVersionRequest
{
    /// <summary>
    /// Optional reason for activating this version (for audit log).
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Response for prompt version history.
/// </summary>
public class PromptVersionHistoryResponse
{
    /// <summary>
    /// The template information.
    /// </summary>
    required public PromptTemplateDto Template { get; init; }

    /// <summary>
    /// List of all versions (ordered by version number descending).
    /// </summary>
    public List<PromptVersionDto> Versions { get; init; } = new();

    /// <summary>
    /// Total number of versions.
    /// </summary>
    public int TotalCount { get; init; }
}

/// <summary>
/// Response for prompt audit log.
/// </summary>
public class PromptAuditLogResponse
{
    /// <summary>
    /// The template information.
    /// </summary>
    required public PromptTemplateDto Template { get; init; }

    /// <summary>
    /// List of audit log entries (ordered by timestamp descending).
    /// </summary>
    public List<PromptAuditLogDto> Logs { get; init; } = new();

    /// <summary>
    /// Total number of log entries.
    /// </summary>
    public int TotalCount { get; init; }
}

/// <summary>
/// Response for listing prompt templates.
/// </summary>
public class PromptTemplateListResponse
{
    /// <summary>
    /// List of templates.
    /// </summary>
    public List<PromptTemplateDto> Templates { get; init; } = new();

    /// <summary>
    /// Total count of templates (before pagination).
    /// </summary>
    public int TotalCount { get; init; }
}
