using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Aggregate root representing a private game owned by a user.
/// Private games are games that are not in the shared catalog and are only visible to the owner.
/// Issue #3662: Phase 1 - Data Model &amp; Core Infrastructure for Private Games.
/// </summary>
public sealed class PrivateGame : AggregateRoot<Guid>
{
    /// <summary>
    /// The ID of the user who owns this private game.
    /// </summary>
    public Guid OwnerId { get; private set; }

    /// <summary>
    /// BoardGameGeek ID if imported from BGG. Null for manual entries.
    /// </summary>
    public int? BggId { get; private set; }

    /// <summary>
    /// The title of the game.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Year the game was published.
    /// </summary>
    public int? YearPublished { get; private set; }

    /// <summary>
    /// Game description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Minimum number of players.
    /// </summary>
    public int MinPlayers { get; private set; }

    /// <summary>
    /// Maximum number of players.
    /// </summary>
    public int MaxPlayers { get; private set; }

    /// <summary>
    /// Typical playing time in minutes.
    /// </summary>
    public int? PlayingTimeMinutes { get; private set; }

    /// <summary>
    /// Minimum recommended age.
    /// </summary>
    public int? MinAge { get; private set; }

    /// <summary>
    /// Complexity rating (1.0-5.0 scale).
    /// </summary>
    public decimal? ComplexityRating { get; private set; }

    /// <summary>
    /// URL to the game's main image.
    /// </summary>
    public string? ImageUrl { get; private set; }

    /// <summary>
    /// URL to the game's thumbnail image.
    /// </summary>
    public string? ThumbnailUrl { get; private set; }

    /// <summary>
    /// Source of the private game data.
    /// </summary>
    public PrivateGameSource Source { get; private set; }

    /// <summary>
    /// When BGG data was last synced (for BGG-sourced private games).
    /// </summary>
    public DateTime? BggSyncedAt { get; private set; }

    // Audit fields
    /// <summary>
    /// When the private game was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When the private game was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

    // Soft delete support
    /// <summary>
    /// Whether the private game has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// When the private game was deleted.
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private PrivateGame() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Private constructor with validation.
    /// </summary>
    private PrivateGame(
        Guid id,
        Guid ownerId,
        string title,
        int minPlayers,
        int maxPlayers,
        PrivateGameSource source) : base(id)
    {
        OwnerId = ownerId;
        Title = title;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
        Source = source;
        CreatedAt = DateTime.UtcNow;
        IsDeleted = false;
    }

    /// <summary>
    /// Creates a new private game from BoardGameGeek data.
    /// </summary>
    /// <param name="ownerId">The ID of the user who owns this game</param>
    /// <param name="bggId">The BoardGameGeek ID</param>
    /// <param name="title">Game title</param>
    /// <param name="yearPublished">Year published</param>
    /// <param name="description">Game description</param>
    /// <param name="minPlayers">Minimum players</param>
    /// <param name="maxPlayers">Maximum players</param>
    /// <param name="playingTimeMinutes">Playing time in minutes</param>
    /// <param name="minAge">Minimum age</param>
    /// <param name="complexityRating">Complexity rating (1.0-5.0)</param>
    /// <param name="imageUrl">Image URL</param>
    /// <param name="thumbnailUrl">Thumbnail URL</param>
    /// <returns>A new PrivateGame instance with BGG data</returns>
    public static PrivateGame CreateFromBgg(
        Guid ownerId,
        int bggId,
        string title,
        int? yearPublished,
        string? description,
        int minPlayers,
        int maxPlayers,
        int? playingTimeMinutes,
        int? minAge,
        decimal? complexityRating,
        string? imageUrl,
        string? thumbnailUrl)
    {
        ValidateOwnerId(ownerId);
        ValidateBggId(bggId);
        ValidateTitle(title);
        ValidatePlayers(minPlayers, maxPlayers);
        ValidateComplexityRating(complexityRating);

        var game = new PrivateGame(
            Guid.NewGuid(),
            ownerId,
            title,
            minPlayers,
            maxPlayers,
            PrivateGameSource.BoardGameGeek)
        {
            BggId = bggId,
            YearPublished = yearPublished,
            Description = description,
            PlayingTimeMinutes = playingTimeMinutes,
            MinAge = minAge,
            ComplexityRating = complexityRating,
            ImageUrl = imageUrl,
            ThumbnailUrl = thumbnailUrl,
            BggSyncedAt = DateTime.UtcNow
        };

        return game;
    }

    /// <summary>
    /// Creates a new private game with manual data entry.
    /// </summary>
    /// <param name="ownerId">The ID of the user who owns this game</param>
    /// <param name="title">Game title (required)</param>
    /// <param name="minPlayers">Minimum players (required)</param>
    /// <param name="maxPlayers">Maximum players (required)</param>
    /// <param name="yearPublished">Year published (optional)</param>
    /// <param name="description">Game description (optional)</param>
    /// <param name="playingTimeMinutes">Playing time in minutes (optional)</param>
    /// <param name="minAge">Minimum age (optional)</param>
    /// <param name="complexityRating">Complexity rating (optional)</param>
    /// <param name="imageUrl">Image URL (optional)</param>
    /// <returns>A new PrivateGame instance with manual data</returns>
    public static PrivateGame CreateManual(
        Guid ownerId,
        string title,
        int minPlayers,
        int maxPlayers,
        int? yearPublished = null,
        string? description = null,
        int? playingTimeMinutes = null,
        int? minAge = null,
        decimal? complexityRating = null,
        string? imageUrl = null)
    {
        ValidateOwnerId(ownerId);
        ValidateTitle(title);
        ValidatePlayers(minPlayers, maxPlayers);
        ValidateComplexityRating(complexityRating);

        var game = new PrivateGame(
            Guid.NewGuid(),
            ownerId,
            title,
            minPlayers,
            maxPlayers,
            PrivateGameSource.Manual)
        {
            YearPublished = yearPublished,
            Description = description,
            PlayingTimeMinutes = playingTimeMinutes,
            MinAge = minAge,
            ComplexityRating = complexityRating,
            ImageUrl = imageUrl
        };

        return game;
    }

    /// <summary>
    /// Updates the game information.
    /// </summary>
    public void UpdateInfo(
        string title,
        int minPlayers,
        int maxPlayers,
        int? yearPublished,
        string? description,
        int? playingTimeMinutes,
        int? minAge,
        decimal? complexityRating,
        string? imageUrl)
    {
        ValidateTitle(title);
        ValidatePlayers(minPlayers, maxPlayers);
        ValidateComplexityRating(complexityRating);

        Title = title;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
        YearPublished = yearPublished;
        Description = description;
        PlayingTimeMinutes = playingTimeMinutes;
        MinAge = minAge;
        ComplexityRating = complexityRating;
        ImageUrl = imageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Syncs the game data from BGG (only for BGG-sourced games).
    /// </summary>
    public void SyncFromBgg(
        string title,
        int? yearPublished,
        string? description,
        int minPlayers,
        int maxPlayers,
        int? playingTimeMinutes,
        int? minAge,
        decimal? complexityRating,
        string? imageUrl,
        string? thumbnailUrl)
    {
        if (Source != PrivateGameSource.BoardGameGeek)
            throw new InvalidOperationException("Cannot sync BGG data for a manually created game");

        ValidateTitle(title);
        ValidatePlayers(minPlayers, maxPlayers);
        ValidateComplexityRating(complexityRating);

        Title = title;
        YearPublished = yearPublished;
        Description = description;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
        PlayingTimeMinutes = playingTimeMinutes;
        MinAge = minAge;
        ComplexityRating = complexityRating;
        ImageUrl = imageUrl;
        ThumbnailUrl = thumbnailUrl;
        BggSyncedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft deletes the private game.
    /// </summary>
    public void Delete()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Game is already deleted");

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Restores a soft-deleted private game.
    /// </summary>
    public void Restore()
    {
        if (!IsDeleted)
            throw new InvalidOperationException("Game is not deleted");

        IsDeleted = false;
        DeletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a data transfer object for promoting this private game to the shared catalog.
    /// This method returns the data needed to create a SharedGame, but does not perform the creation.
    /// </summary>
    /// <returns>A record containing the data for SharedGame creation</returns>
    public PrivateGamePromotionData ToSharedGame()
    {
        return new PrivateGamePromotionData(
            BggId: BggId,
            Title: Title,
            YearPublished: YearPublished ?? DateTime.UtcNow.Year,
            Description: Description ?? string.Empty,
            MinPlayers: MinPlayers,
            MaxPlayers: MaxPlayers,
            PlayingTimeMinutes: PlayingTimeMinutes ?? 60,
            MinAge: MinAge ?? 0,
            ComplexityRating: ComplexityRating,
            ImageUrl: ImageUrl ?? string.Empty,
            ThumbnailUrl: ThumbnailUrl ?? ImageUrl ?? string.Empty,
            OriginalOwnerId: OwnerId,
            PrivateGameId: Id
        );
    }

    // Validation methods
    private static void ValidateOwnerId(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("OwnerId cannot be empty", nameof(ownerId));
    }

    private static void ValidateBggId(int bggId)
    {
        if (bggId <= 0)
            throw new ArgumentException("BggId must be a positive integer", nameof(bggId));
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (title.Length > 200)
            throw new ArgumentException("Title cannot exceed 200 characters", nameof(title));
    }

    private static void ValidatePlayers(int minPlayers, int maxPlayers)
    {
        if (minPlayers < 1)
            throw new ArgumentException("MinPlayers must be at least 1", nameof(minPlayers));

        if (minPlayers > 100)
            throw new ArgumentException("MinPlayers cannot exceed 100", nameof(minPlayers));

        if (maxPlayers < minPlayers)
            throw new ArgumentException("MaxPlayers must be greater than or equal to MinPlayers", nameof(maxPlayers));

        if (maxPlayers > 100)
            throw new ArgumentException("MaxPlayers cannot exceed 100", nameof(maxPlayers));
    }

    private static void ValidateComplexityRating(decimal? rating)
    {
        if (rating.HasValue && (rating.Value < 1.0m || rating.Value > 5.0m))
            throw new ArgumentException("ComplexityRating must be between 1.0 and 5.0", nameof(rating));
    }
}

/// <summary>
/// Data transfer object containing the data needed to promote a private game to the shared catalog.
/// </summary>
/// <param name="BggId">BoardGameGeek ID if available</param>
/// <param name="Title">Game title</param>
/// <param name="YearPublished">Year published</param>
/// <param name="Description">Game description</param>
/// <param name="MinPlayers">Minimum players</param>
/// <param name="MaxPlayers">Maximum players</param>
/// <param name="PlayingTimeMinutes">Playing time in minutes</param>
/// <param name="MinAge">Minimum age</param>
/// <param name="ComplexityRating">Complexity rating</param>
/// <param name="ImageUrl">Image URL</param>
/// <param name="ThumbnailUrl">Thumbnail URL</param>
/// <param name="OriginalOwnerId">ID of the user who originally created this private game</param>
/// <param name="PrivateGameId">ID of the source private game</param>
public record PrivateGamePromotionData(
    int? BggId,
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    string ImageUrl,
    string ThumbnailUrl,
    Guid OriginalOwnerId,
    Guid PrivateGameId
);
