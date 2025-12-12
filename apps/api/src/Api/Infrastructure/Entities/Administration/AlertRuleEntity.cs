using System;

namespace Api.Infrastructure.Entities.Administration;

/// <summary>
/// Alert rule entity for dynamic alert configuration (Issue #921)
/// Stores user-defined alert rules that can be evaluated at runtime.
/// </summary>
public class AlertRuleEntity
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Unique name for this alert rule (e.g., "High Error Rate Production")
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Type of alert (e.g., "HighErrorRate", "HighLatency", "QdrantDown")
    /// Maps to predefined alert types in the system.
    /// </summary>
    public required string AlertType { get; set; }
    
    /// <summary>
    /// Severity level: Info, Warning, Error, Critical
    /// </summary>
    public required string Severity { get; set; }
    
    /// <summary>
    /// Description of what this alert monitors
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Threshold value (e.g., 0.05 for 5%, 1000 for 1000ms)
    /// </summary>
    public double Threshold { get; set; }
    
    /// <summary>
    /// Threshold unit (e.g., "%", "ms", "count", "bytes")
    /// </summary>
    public required string ThresholdUnit { get; set; }
    
    /// <summary>
    /// Duration in minutes that threshold must be exceeded before alerting
    /// </summary>
    public int DurationMinutes { get; set; }
    
    /// <summary>
    /// Whether this rule is currently active
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// JSON metadata for additional rule configuration
    /// </summary>
    public string? Metadata { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public required string CreatedBy { get; set; }
    public required string UpdatedBy { get; set; }
}
