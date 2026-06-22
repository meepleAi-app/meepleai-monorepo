namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO for a trending game in the catalog.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
public sealed record TrendingGameDto
{
    /// <summary>
    /// Rank position in the trending list (1-based).
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// The shared game ID.
    /// </summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// Game title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Game thumbnail URL.
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// Computed trending score (weighted + time-decayed).
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Number of search events in the time window.
    /// </summary>
    public int SearchCount { get; init; }

    /// <summary>
    /// Number of view events in the time window.
    /// </summary>
    public int ViewCount { get; init; }

    /// <summary>
    /// Number of library addition events in the time window.
    /// </summary>
    public int LibraryAddCount { get; init; }

    /// <summary>
    /// Number of play session events in the time window.
    /// </summary>
    public int PlayCount { get; init; }

    /// <summary>
    /// Issue #2290: mirrors <see cref="Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity.HasKnowledgeBase"/>
    /// so Discover row 1 can render the "AI Ready" / KB badge inline (Block B
    /// of <see href="https://github.com/meepleAi-app/meepleai-monorepo/issues/2289">#2289</see>)
    /// without an N+1 lookup on <see cref="SharedGameDto"/>.
    /// </summary>
    public bool HasKnowledgeBase { get; init; }
}
