using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector; // Issue #3547: Vector type for pgvector columns

namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for conversation memory persistence.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
[Table("conversation_memory")]
public class ConversationMemoryEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("session_id")]
    public Guid SessionId { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("game_id")]
    public Guid? GameId { get; set; }

    [Required]
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Required]
    [Column("message_type")]
    [MaxLength(50)]
    public string MessageType { get; set; } = string.Empty;

    [Required]
    [Column("timestamp")]
    public DateTime Timestamp { get; set; }

    [Column("embedding", TypeName = "vector(1536)")]
    public Vector? Embedding { get; set; }

    // Navigation properties
    public UserEntity? User { get; set; }
}
