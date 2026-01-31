namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Entity for game errata.
/// Persistence model for GameErrata entity.
/// </summary>
public class GameErrataEntity
{
    public Guid Id { get; set; }
    public Guid SharedGameId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string PageReference { get; set; } = string.Empty;
    public DateTime PublishedDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public SharedGameEntity SharedGame { get; set; } = default!;
}
