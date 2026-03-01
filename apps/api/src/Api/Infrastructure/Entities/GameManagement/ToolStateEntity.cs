using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for ToolState.
/// Stores runtime tool state as JSONB in a live game session.
/// Issue #4754: ToolState Entity + Toolkit ↔ Session Integration.
/// </summary>
public class ToolStateEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid ToolkitId { get; set; }

    [MaxLength(200)]
    public string ToolName { get; set; } = string.Empty;

    public int ToolType { get; set; }

    /// <summary>
    /// Runtime state stored as JSONB for efficient querying.
    /// </summary>
    public string StateDataJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
