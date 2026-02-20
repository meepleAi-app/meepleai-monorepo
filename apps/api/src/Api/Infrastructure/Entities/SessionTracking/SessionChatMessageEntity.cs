using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for SessionChatMessage (EF Core mapping).
/// Maps to session_tracking_chat_messages table.
/// Issue #4760
/// </summary>
public class SessionChatMessageEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid? SenderId { get; set; }

    [MaxLength(5000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(20)]
    public string MessageType { get; set; } = string.Empty;

    public int? TurnNumber { get; set; }
    public int SequenceNumber { get; set; }

    // Agent metadata
    [MaxLength(50)]
    public string? AgentType { get; set; }

    public float? Confidence { get; set; }
    public string? CitationsJson { get; set; }

    // Mentions
    public string? MentionsJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public SessionEntity? Session { get; set; }
    public ParticipantEntity? Sender { get; set; }
}
