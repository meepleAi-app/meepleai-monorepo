namespace Api.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing the core descriptive data shared between
/// SharedGame (SharedGameCatalog BC) and PrivateGame (UserLibrary BC).
/// Issue #1320: extracted to eliminate field duplication across the two aggregate roots.
/// </summary>
public sealed record GameCoreData
{
    /// <summary>The game title (trimmed, non-empty).</summary>
    public string Title { get; }

    /// <summary>Year the game was first published.</summary>
    public int YearPublished { get; }

    /// <summary>Minimum number of players (1..100).</summary>
    public int MinPlayers { get; }

    /// <summary>Maximum number of players (&gt;= MinPlayers, &lt;= 100).</summary>
    public int MaxPlayers { get; }

    /// <summary>Typical playing time in minutes (non-negative).</summary>
    public int PlayingTimeMinutes { get; }

    /// <summary>Minimum recommended player age.</summary>
    public int MinAge { get; }

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; }

    /// <summary>Optional URL for the full-size game image.</summary>
    public string? ImageUrl { get; }

    /// <summary>Optional URL for the thumbnail image.</summary>
    public string? ThumbnailUrl { get; }

    /// <summary>Optional BoardGameGeek identifier.</summary>
    public int? BggId { get; }

    /// <summary>Optional BGG complexity (weight) rating.</summary>
    public decimal? ComplexityRating { get; }

    private GameCoreData(
        string title,
        int yearPublished,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        string? description,
        string? imageUrl,
        string? thumbnailUrl,
        int? bggId,
        decimal? complexityRating)
    {
        Title = title;
        YearPublished = yearPublished;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
        PlayingTimeMinutes = playingTimeMinutes;
        MinAge = minAge;
        Description = description;
        ImageUrl = imageUrl;
        ThumbnailUrl = thumbnailUrl;
        BggId = bggId;
        ComplexityRating = complexityRating;
    }

    /// <summary>
    /// Creates a validated <see cref="GameCoreData"/> instance.
    /// </summary>
    /// <param name="title">Non-empty game title (whitespace is trimmed).</param>
    /// <param name="yearPublished">Year of first publication.</param>
    /// <param name="minPlayers">Minimum players (1..100).</param>
    /// <param name="maxPlayers">Maximum players (&gt;= minPlayers, &lt;= 100).</param>
    /// <param name="playingTimeMinutes">Playing time in minutes (>= 0).</param>
    /// <param name="minAge">Minimum recommended age.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="imageUrl">Optional full-size image URL.</param>
    /// <param name="thumbnailUrl">Optional thumbnail URL.</param>
    /// <param name="bggId">Optional BoardGameGeek ID.</param>
    /// <param name="complexityRating">Optional BGG complexity rating.</param>
    /// <returns>A new, immutable <see cref="GameCoreData"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="title"/> is empty or whitespace,
    /// <paramref name="minPlayers"/> is out of range, <paramref name="maxPlayers"/>
    /// is less than <paramref name="minPlayers"/>, or <paramref name="playingTimeMinutes"/>
    /// is negative.
    /// </exception>
    public static GameCoreData Create(
        string title,
        int yearPublished,
        int minPlayers,
        int maxPlayers,
        int playingTimeMinutes,
        int minAge,
        string? description = null,
        string? imageUrl = null,
        string? thumbnailUrl = null,
        int? bggId = null,
        decimal? complexityRating = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("title cannot be empty or whitespace", nameof(title));

        if (minPlayers < 1 || minPlayers > 100)
            throw new ArgumentException("minPlayers must be in range 1..100", nameof(minPlayers));

        if (maxPlayers < minPlayers || maxPlayers > 100)
            throw new ArgumentException(
                $"maxPlayers ({maxPlayers}) must be >= minPlayers ({minPlayers}) and <= 100",
                nameof(maxPlayers));

        if (playingTimeMinutes < 0)
            throw new ArgumentException(
                "playingTimeMinutes must be non-negative",
                nameof(playingTimeMinutes));

        return new GameCoreData(
            title.Trim(),
            yearPublished,
            minPlayers,
            maxPlayers,
            playingTimeMinutes,
            minAge,
            description,
            imageUrl,
            thumbnailUrl,
            bggId,
            complexityRating);
    }

    /// <summary>
    /// Returns a new <see cref="GameCoreData"/> with the title replaced.
    /// All other fields are unchanged.
    /// </summary>
    public GameCoreData WithTitle(string newTitle) =>
        Create(newTitle, YearPublished, MinPlayers, MaxPlayers, PlayingTimeMinutes, MinAge,
               Description, ImageUrl, ThumbnailUrl, BggId, ComplexityRating);
}
