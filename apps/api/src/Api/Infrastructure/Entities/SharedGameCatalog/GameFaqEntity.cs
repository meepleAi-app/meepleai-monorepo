namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Entity for game FAQs.
/// Persistence model for GameFaq entity.
/// </summary>
public class GameFaqEntity
{
    public Guid Id { get; set; }
    public Guid SharedGameId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }

    public SharedGameEntity SharedGame { get; set; } = default!;
}
