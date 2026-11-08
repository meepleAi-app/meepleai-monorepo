using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core entity for game session state persistence.
/// Stores the complete game state as JSON conforming to GameStateTemplate schema.
/// </summary>
public class GameSessionStateEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameSessionId { get; set; }
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Current game state stored as JSONB for efficient querying.
    /// </summary>
    public string CurrentStateJson { get; set; } = "{}";

    /// <summary>
    /// Version for optimistic concurrency control.
    /// </summary>
    public int Version { get; set; } = 1;

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(255)]
    public string LastUpdatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Concurrency token (#3651, ADR-060) sulla colonna di sistema <c>xmin</c>.
    ///
    /// <para>
    /// Il commento precedente diceva «Optimistic locking via PostgreSQL xmin (EF Core Timestamp)»:
    /// <b>non era vero</b>. <c>[Timestamp]</c> su un <c>byte[]</c> produce una colonna <c>bytea</c>
    /// che Postgres non popola, quindi EF confrontava <c>NULL = NULL</c> e nessun conflitto veniva
    /// rilevato. È la quarta affermazione di questo tipo trovata da #3651.
    /// </para>
    /// </summary>
    public uint Xmin { get; set; }

    // Navigation properties
    public GameSessionEntity GameSession { get; set; } = null!;
    public ICollection<GameStateSnapshotEntity> Snapshots { get; set; } = new List<GameStateSnapshotEntity>();
}
