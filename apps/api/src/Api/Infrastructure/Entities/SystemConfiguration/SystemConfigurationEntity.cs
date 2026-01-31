namespace Api.Infrastructure.Entities;

/// <summary>
/// Database entity for storing system-wide configuration values.
/// Supports dynamic runtime configuration changes without redeployment.
/// </summary>
public class SystemConfigurationEntity
{
    /// <summary>
    /// Unique identifier for the configuration entry.
    /// </summary>
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Configuration key in hierarchical format (e.g., "RateLimit:Admin:MaxTokens").
    /// Uses colon-separated hierarchy matching ASP.NET Core configuration convention.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// Configuration value stored as JSON.
    /// Supports primitive types (string, int, bool) and complex objects.
    /// </summary>
    public string Value { get; set; } = default!;

    /// <summary>
    /// Type of the configuration value (e.g., "string", "int", "bool", "json").
    /// Used for type-safe deserialization and validation.
    /// </summary>
    public string ValueType { get; set; } = "string";

    /// <summary>
    /// Human-readable description of the configuration.
    /// Helps administrators understand the purpose and impact of changes.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category for grouping related configurations (e.g., "RateLimit", "AI", "Cache").
    /// Enables filtering and organization in admin UI.
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Whether this configuration is currently active.
    /// Inactive configurations are not applied to the system.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this configuration requires application restart to take effect.
    /// True for configurations that cannot be hot-reloaded.
    /// </summary>
    public bool RequiresRestart { get; set; }

    /// <summary>
    /// Environment where this configuration applies (e.g., "Development", "Production", "All").
    /// Enables environment-specific configuration overrides.
    /// </summary>
    public string Environment { get; set; } = "All";

    /// <summary>
    /// Version number for this configuration entry.
    /// Incremented on each update for change tracking and rollback capability.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Previous value before the last update (stored as JSON).
    /// Enables quick rollback without accessing audit history.
    /// </summary>
    public string? PreviousValue { get; set; }

    /// <summary>
    /// When the configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID of the administrator who created this configuration.
    /// </summary>
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// User ID of the administrator who last updated this configuration.
    /// </summary>
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>
    /// When the configuration last changed from inactive to active or vice versa.
    /// Used for tracking configuration lifecycle events.
    /// </summary>
    public DateTime? LastToggledAt { get; set; }

    // Navigation properties
    public UserEntity CreatedBy { get; set; } = default!;
    public UserEntity? UpdatedBy { get; set; }
}
