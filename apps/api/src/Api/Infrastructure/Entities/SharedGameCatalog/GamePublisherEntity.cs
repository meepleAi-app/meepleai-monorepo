namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Entity for game publishers.
/// Persistence model for GamePublisher entity.
/// </summary>
public class GamePublisherEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<SharedGameEntity> SharedGames { get; set; } = new List<SharedGameEntity>();
}
