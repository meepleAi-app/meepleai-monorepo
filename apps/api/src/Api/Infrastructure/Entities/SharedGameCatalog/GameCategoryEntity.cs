namespace Api.Infrastructure.Entities.SharedGameCatalog;

public class GameCategoryEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Emoji { get; set; }
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public ICollection<SharedGameEntity> SharedGames { get; set; } = new List<SharedGameEntity>();
}
