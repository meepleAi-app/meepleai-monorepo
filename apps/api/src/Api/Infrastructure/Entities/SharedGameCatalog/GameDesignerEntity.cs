namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Entity for game designers.
/// Persistence model for GameDesigner entity.
/// </summary>
public class GameDesignerEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<SharedGameEntity> SharedGames { get; set; } = new List<SharedGameEntity>();
}
