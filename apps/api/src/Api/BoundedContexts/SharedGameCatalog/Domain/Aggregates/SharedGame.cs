using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a shared game in the catalog.
/// Manages game core information, gameplay details, and publication status.
/// </summary>
public sealed class SharedGame : AggregateRoot<Guid>
{
    // Identity
    private Guid _id;
    private int? _bggId;

    // Core Info
    private string _title = string.Empty;
    private int _yearPublished;
    private string _description = string.Empty;

    // Gameplay
    private int _minPlayers;
    private int _maxPlayers;
    private int _playingTimeMinutes;
    private int _minAge;

    // Ratings
    private decimal? _complexityRating;
    private decimal? _averageRating;

    // Media
    private string _imageUrl = string.Empty;
    private string _thumbnailUrl = string.Empty;

    // Rules
    private GameRules? _rules;

    // Status & Metadata
    private GameStatus _status;
    private Guid _createdBy;
    private Guid? _modifiedBy;
    private readonly DateTime _createdAt;
    private DateTime? _modifiedAt;

    // Full-Text Search (populated by PostgreSQL trigger)
    private readonly string _searchVector = string.Empty;

    /// <summary>
    /// Gets the unique identifier of this game.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the BoardGameGeek ID if imported from BGG.
    /// </summary>
    public int? BggId => _bggId;

    /// <summary>
    /// Gets the game title.
    /// </summary>
    public string Title => _title;

    /// <summary>
    /// Gets the year the game was published.
    /// </summary>
    public int YearPublished => _yearPublished;

    /// <summary>
    /// Gets the game description.
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// Gets the minimum number of players.
    /// </summary>
    public int MinPlayers => _minPlayers;

    /// <summary>
    /// Gets the maximum number of players.
    /// </summary>
    public int MaxPlayers => _maxPlayers;

    /// <summary>
    /// Gets the typical playing time in minutes.
    /// </summary>
    public int PlayingTimeMinutes => _playingTimeMinutes;

    /// <summary>
    /// Gets the minimum recommended age.
    /// </summary>
    public int MinAge => _minAge;

    /// <summary>
    /// Gets the complexity rating (1.0 - 5.0).
    /// </summary>
    public decimal? ComplexityRating => _complexityRating;

    /// <summary>
    /// Gets the average user rating (1.0 - 10.0).
    /// </summary>
    public decimal? AverageRating => _averageRating;

    /// <summary>
    /// Gets the URL to the game's main image.
    /// </summary>
    public string ImageUrl => _imageUrl;

    /// <summary>
    /// Gets the URL to the game's thumbnail image.
    /// </summary>
    public string ThumbnailUrl => _thumbnailUrl;

    /// <summary>
    /// Gets the game rules.
    /// </summary>
    public GameRules? Rules => _rules;

    /// <summary>
    /// Gets the publication status of this game.
    /// </summary>
    public GameStatus Status => _status;

    /// <summary>
    /// Gets the ID of the user who created this game entry.
    /// </summary>
    public Guid CreatedBy => _createdBy;

    /// <summary>
    /// Gets the ID of the user who last modified this game entry.
    /// </summary>
    public Guid? ModifiedBy => _modifiedBy;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Gets the last modification timestamp.
    /// </summary>
    public DateTime? ModifiedAt => _modifiedAt;

    /// <summary>
    /// Gets the full-text search vector (managed by PostgreSQL).
    /// </summary>
    public string SearchVector => _searchVector;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private SharedGame() : base()
    {
    }

    /// <summary>
    /// Private constructor for creating a new SharedGame instance (with validation and events).
    /// </summary>
    private SharedGame(
        Guid id,
        string title,
        int yearPublished,
        string description,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        decimal? complexityRating,
        decimal? averageRating,
        string imageUrl,
        string thumbnailUrl,
        GameRules? rules,
        Guid createdBy,
        int? bggId = null) : base(id)
    {
        _id = id;
        _title = title;
        _yearPublished = yearPublished;
        _description = description;
        _minPlayers = minPlayers;
        _maxPlayers = maxPlayers;
        _playingTimeMinutes = playingTimeMinutes;
        _minAge = minAge;
        _complexityRating = complexityRating;
        _averageRating = averageRating;
        _imageUrl = imageUrl;
        _thumbnailUrl = thumbnailUrl;
        _rules = rules;
        _status = GameStatus.Draft;
        _createdBy = createdBy;
        _createdAt = DateTime.UtcNow;
        _bggId = bggId;
    }

    /// <summary>
    /// Internal constructor for reconstituting a SharedGame from persistence.
    /// Used only by the repository. Does not raise domain events.
    /// </summary>
    internal SharedGame(
        Guid id,
        string title,
        int yearPublished,
        string description,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        decimal? complexityRating,
        decimal? averageRating,
        string imageUrl,
        string thumbnailUrl,
        GameRules? rules,
        GameStatus status,
        Guid createdBy,
        Guid? modifiedBy,
        DateTime createdAt,
        DateTime? modifiedAt,
        int? bggId = null) : base(id)
    {
        _id = id;
        _title = title;
        _yearPublished = yearPublished;
        _description = description;
        _minPlayers = minPlayers;
        _maxPlayers = maxPlayers;
        _playingTimeMinutes = playingTimeMinutes;
        _minAge = minAge;
        _complexityRating = complexityRating;
        _averageRating = averageRating;
        _imageUrl = imageUrl;
        _thumbnailUrl = thumbnailUrl;
        _rules = rules;
        _status = status;
        _createdBy = createdBy;
        _modifiedBy = modifiedBy;
        _createdAt = createdAt;
        _modifiedAt = modifiedAt;
        _bggId = bggId;
    }

    /// <summary>
    /// Creates a new SharedGame with validation.
    /// </summary>
    /// <returns>A new SharedGame instance in Draft status</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static SharedGame Create(
        string title,
        int yearPublished,
        string description,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        decimal? complexityRating,
        decimal? averageRating,
        string imageUrl,
        string thumbnailUrl,
        GameRules? rules,
        Guid createdBy,
        int? bggId = null)
    {
        // Validation
        ValidateTitle(title);
        ValidateYear(yearPublished);
        ValidateDescription(description);
        ValidatePlayers(minPlayers, maxPlayers);
        ValidatePlayingTime(playingTimeMinutes);
        ValidateMinAge(minAge);
        ValidateComplexityRating(complexityRating);
        ValidateAverageRating(averageRating);
        ValidateImageUrl(imageUrl);
        ValidateThumbnailUrl(thumbnailUrl);

        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        var gameId = Guid.NewGuid();
        var game = new SharedGame(
            gameId,
            title,
            yearPublished,
            description,
            minPlayers,
            maxPlayers,
            playingTimeMinutes,
            minAge,
            complexityRating,
            averageRating,
            imageUrl,
            thumbnailUrl,
            rules,
            createdBy,
            bggId);

        game.AddDomainEvent(new SharedGameCreatedEvent(gameId, title, createdBy));

        return game;
    }

    /// <summary>
    /// Updates the game information.
    /// </summary>
    public void UpdateInfo(
        string title,
        int yearPublished,
        string description,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        decimal? complexityRating,
        decimal? averageRating,
        string imageUrl,
        string thumbnailUrl,
        GameRules? rules,
        Guid modifiedBy)
    {
        // Validation
        ValidateTitle(title);
        ValidateYear(yearPublished);
        ValidateDescription(description);
        ValidatePlayers(minPlayers, maxPlayers);
        ValidatePlayingTime(playingTimeMinutes);
        ValidateMinAge(minAge);
        ValidateComplexityRating(complexityRating);
        ValidateAverageRating(averageRating);
        ValidateImageUrl(imageUrl);
        ValidateThumbnailUrl(thumbnailUrl);

        if (modifiedBy == Guid.Empty)
            throw new ArgumentException("ModifiedBy cannot be empty", nameof(modifiedBy));

        _title = title;
        _yearPublished = yearPublished;
        _description = description;
        _minPlayers = minPlayers;
        _maxPlayers = maxPlayers;
        _playingTimeMinutes = playingTimeMinutes;
        _minAge = minAge;
        _complexityRating = complexityRating;
        _averageRating = averageRating;
        _imageUrl = imageUrl;
        _thumbnailUrl = thumbnailUrl;
        _rules = rules;
        _modifiedBy = modifiedBy;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SharedGameUpdatedEvent(_id, modifiedBy));
    }

    /// <summary>
    /// Publishes the game, making it visible to all users.
    /// </summary>
    /// <param name="publishedBy">The ID of the user publishing the game</param>
    /// <exception cref="InvalidOperationException">Thrown when game is not in Draft status</exception>
    public void Publish(Guid publishedBy)
    {
        if (_status != GameStatus.Draft)
            throw new InvalidOperationException($"Cannot publish game in {_status} status");

        if (publishedBy == Guid.Empty)
            throw new ArgumentException("PublishedBy cannot be empty", nameof(publishedBy));

        _status = GameStatus.Published;
        _modifiedBy = publishedBy;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SharedGamePublishedEvent(_id, publishedBy));
    }

    /// <summary>
    /// Archives the game, removing it from public view.
    /// </summary>
    /// <param name="archivedBy">The ID of the user archiving the game</param>
    /// <exception cref="InvalidOperationException">Thrown when game is already archived</exception>
    public void Archive(Guid archivedBy)
    {
        if (_status == GameStatus.Archived)
            throw new InvalidOperationException("Game is already archived");

        if (archivedBy == Guid.Empty)
            throw new ArgumentException("ArchivedBy cannot be empty", nameof(archivedBy));

        _status = GameStatus.Archived;
        _modifiedBy = archivedBy;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new SharedGameArchivedEvent(_id, archivedBy));
    }

    // Validation Methods

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (title.Length > 500)
            throw new ArgumentException("Title cannot exceed 500 characters", nameof(title));
    }

    private static void ValidateYear(int year)
    {
        if (year <= 1900)
            throw new ArgumentException("Year must be greater than 1900", nameof(year));

        if (year > DateTime.UtcNow.Year + 1)
            throw new ArgumentException($"Year cannot exceed {DateTime.UtcNow.Year + 1}", nameof(year));
    }

    private static void ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));
    }

    private static void ValidatePlayers(int minPlayers, int maxPlayers)
    {
        if (minPlayers <= 0)
            throw new ArgumentException("MinPlayers must be greater than 0", nameof(minPlayers));

        if (maxPlayers < minPlayers)
            throw new ArgumentException("MaxPlayers must be greater than or equal to MinPlayers", nameof(maxPlayers));
    }

    private static void ValidatePlayingTime(int playingTimeMinutes)
    {
        if (playingTimeMinutes <= 0)
            throw new ArgumentException("PlayingTime must be greater than 0", nameof(playingTimeMinutes));
    }

    private static void ValidateMinAge(int minAge)
    {
        if (minAge < 0)
            throw new ArgumentException("MinAge cannot be negative", nameof(minAge));
    }

    private static void ValidateComplexityRating(decimal? rating)
    {
        if (rating.HasValue && (rating.Value < 1.0m || rating.Value > 5.0m))
            throw new ArgumentException("ComplexityRating must be between 1.0 and 5.0", nameof(rating));
    }

    private static void ValidateAverageRating(decimal? rating)
    {
        if (rating.HasValue && (rating.Value < 1.0m || rating.Value > 10.0m))
            throw new ArgumentException("AverageRating must be between 1.0 and 10.0", nameof(rating));
    }

    private static void ValidateImageUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ArgumentException("ImageUrl is required", nameof(imageUrl));

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            throw new ArgumentException("ImageUrl must be a valid URL", nameof(imageUrl));
    }

    private static void ValidateThumbnailUrl(string thumbnailUrl)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
            throw new ArgumentException("ThumbnailUrl is required", nameof(thumbnailUrl));

        if (!Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out _))
            throw new ArgumentException("ThumbnailUrl must be a valid URL", nameof(thumbnailUrl));
    }
}
