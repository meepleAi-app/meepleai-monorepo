using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core entity for game state snapshot persistence.
/// Stores historical snapshots for undo/redo functionality.
/// </summary>
public class GameStateSnapshotEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionStateId { get; set; }

    /// <summary>
    /// Snapshot state stored as JSONB.
    /// </summary>
    public string StateJson { get; set; } = "{}";

    public int TurnNumber { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(255)]
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation property
    public GameSessionStateEntity SessionState { get; set; } = null!;
}
