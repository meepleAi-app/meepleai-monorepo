namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Entity for shared games in the catalog.
/// Persistence model for SharedGame aggregate root.
/// </summary>
public class SharedGameEntity
{
    public Guid Id { get; set; }
    public int? BggId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int YearPublished { get; set; }
    public string Description { get; set; } = string.Empty;
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int PlayingTimeMinutes { get; set; }
    public int MinAge { get; set; }
    public decimal? ComplexityRating { get; set; }
    public decimal? AverageRating { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public int Status { get; set; } // 0=Draft, 1=Published, 2=Archived
    public string? RulesContent { get; set; }
    public string? RulesLanguage { get; set; }
    // SearchVector managed by PostgreSQL trigger - not mapped by EF Core
    public Guid CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation properties (many-to-many)
    public ICollection<GameDesignerEntity> Designers { get; set; } = new List<GameDesignerEntity>();
    public ICollection<GamePublisherEntity> Publishers { get; set; } = new List<GamePublisherEntity>();
    public ICollection<GameCategoryEntity> Categories { get; set; } = new List<GameCategoryEntity>();
    public ICollection<GameMechanicEntity> Mechanics { get; set; } = new List<GameMechanicEntity>();

    // Navigation properties (one-to-many)
    public ICollection<GameFaqEntity> Faqs { get; set; } = new List<GameFaqEntity>();
    public ICollection<GameErrataEntity> Erratas { get; set; } = new List<GameErrataEntity>();
}
