namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for game strategies.
/// Issue #4903: Game strategies API endpoint.
/// </summary>
public class GameStrategyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Cross-BC reference to SharedGameCatalog.shared_games.id.
    /// </summary>
    public Guid SharedGameId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int Upvotes { get; set; }

    /// <summary>
    /// Tags stored as JSON array string, e.g. ["opening","defense"].
    /// </summary>
    public string Tags { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
