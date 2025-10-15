namespace Api.Infrastructure.Entities;

public class RuleSpecCommentEntity
{
    public Guid Id { get; set; }
        = Guid.NewGuid();
    public string GameId { get; set; } = default!;
    public string Version { get; set; } = default!;
    public string? AtomId { get; set; }
    public string UserId { get; set; } = default!;
    public string CommentText { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public GameEntity Game { get; set; } = default!;
    public UserEntity User { get; set; } = default!;
}
