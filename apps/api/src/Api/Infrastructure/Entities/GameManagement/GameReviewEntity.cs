namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core entity for GameReview persistence.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
public class GameReviewEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SharedGameId { get; set; }
    public Guid UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
