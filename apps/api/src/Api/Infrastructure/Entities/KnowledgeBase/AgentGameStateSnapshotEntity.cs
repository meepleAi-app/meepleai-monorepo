using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector; // Issue #3547: Vector type for pgvector columns

namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for agent game state snapshot persistence.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// Issue #3547: Renamed table to avoid conflict with GameManagement's GameStateSnapshotEntity
/// </summary>
[Table("agent_game_state_snapshots")]
public class AgentGameStateSnapshotEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("game_id")]
    public Guid GameId { get; set; }

    [Required]
    [Column("agent_session_id")]
    public Guid AgentSessionId { get; set; }

    [Required]
    [Column("board_state_json", TypeName = "jsonb")]
    public string BoardStateJson { get; set; } = "{}";

    [Required]
    [Column("turn_number")]
    public int TurnNumber { get; set; }

    [Column("active_player_id")]
    public Guid? ActivePlayerId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("embedding", TypeName = "vector(1024)")]
    public Vector? Embedding { get; set; }

    // Navigation properties
    public AgentSessionEntity? AgentSession { get; set; }
}
