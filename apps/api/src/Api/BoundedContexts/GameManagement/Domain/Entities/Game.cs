using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Game aggregate root representing a board game in the system.
/// </summary>
internal sealed class Game : AggregateRoot<Guid>
{
    public GameTitle Title { get; private set; }
    public Publisher? Publisher { get; private set; }
    public YearPublished? YearPublished { get; private set; }
    public PlayerCount? PlayerCount { get; private set; }
    public PlayTime? PlayTime { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // BGG Integration (AI-13)
    public int? BggId { get; private set; }
    public string? BggMetadata { get; private set; }

    // SharedGameCatalog Integration (Issue #2373)
    public Guid? SharedGameId { get; private set; }

    // Admin Wizard: Game images
    public string? IconUrl { get; private set; }
    public string? ImageUrl { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private Game() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new game with required title.
    /// </summary>
    public Game(
        Guid id,
        GameTitle title,
        Publisher? publisher = null,
        YearPublished? yearPublished = null,
        PlayerCount? playerCount = null,
        PlayTime? playTime = null) : base(id)
    {
        ArgumentNullException.ThrowIfNull(title);
        Title = title;
        Publisher = publisher;
        YearPublished = yearPublished;
        PlayerCount = playerCount;
        PlayTime = playTime;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new GameCreatedEvent(id, title.Value, null));
    }

    /// <summary>
    /// Updates game details.
    /// </summary>
    public void UpdateDetails(
        GameTitle? title = null,
        Publisher? publisher = null,
        YearPublished? yearPublished = null,
        PlayerCount? playerCount = null,
        PlayTime? playTime = null)
    {
        if (title != null) Title = title;
        if (publisher != null) Publisher = publisher;
        if (yearPublished != null) YearPublished = yearPublished;
        if (playerCount != null) PlayerCount = playerCount;
        if (playTime != null) PlayTime = playTime;

        AddDomainEvent(new GameUpdatedEvent(Id, Title.Value));
    }

    /// <summary>
    /// Sets game images (icon and cover image).
    /// </summary>
    public void SetImages(string? iconUrl, string? imageUrl)
    {
        IconUrl = iconUrl;
        ImageUrl = imageUrl;
    }

    /// <summary>
    /// Links game to BoardGameGeek entry.
    /// </summary>
    public void LinkToBgg(int bggId, string? metadata = null)
    {
        if (bggId <= 0)
            throw new ArgumentException("BGG ID must be positive", nameof(bggId));

        BggId = bggId;
        BggMetadata = metadata;

        AddDomainEvent(new GameLinkedToBggEvent(Id, bggId));
    }

    /// <summary>
    /// Links game to SharedGameCatalog entry.
    /// Issue #2373: Enables enriched game data from community catalog.
    /// </summary>
    public void LinkToSharedGame(Guid sharedGameId)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        SharedGameId = sharedGameId;
    }

    /// <summary>
    /// Checks if this game supports the given player count.
    /// </summary>
    public bool SupportsPlayerCount(int players)
    {
        return PlayerCount?.Supports(players) ?? true; // If not specified, assume yes
    }

    /// <summary>
    /// Checks if game is suitable for solo play.
    /// </summary>
    public bool SupportsSolo => PlayerCount?.SupportsSolo ?? false;
}
