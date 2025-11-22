namespace Api.Infrastructure.Entities;

public class RuleSpecCommentEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid GameId { get; set; }
    public string Version { get; set; } = default!;
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid? AtomId { get; set; }
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid UserId { get; set; }
    public string CommentText { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // EDIT-05: Inline Annotations
    public int? LineNumber { get; set; }
    public string? LineContext { get; set; }

    // EDIT-05: Comment Threading
    public Guid? ParentCommentId { get; set; }

    // EDIT-05: Resolution Tracking
    public bool IsResolved { get; set; } = false;
    // DDD-PHASE2: Converted to Guid for domain alignment
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // EDIT-05: User Mentions
    // DDD-PHASE2: Converted to Guid for domain alignment
    public List<Guid> MentionedUserIds { get; set; } = new();

    // Navigation Properties
    public GameEntity Game { get; set; } = default!;
    public UserEntity User { get; set; } = default!;

    // EDIT-05: Threading Navigation Properties
    public RuleSpecCommentEntity? ParentComment { get; set; }
    public List<RuleSpecCommentEntity> Replies { get; set; } = new();

    // EDIT-05: Resolution Navigation Property
    public UserEntity? ResolvedByUser { get; set; }
}
