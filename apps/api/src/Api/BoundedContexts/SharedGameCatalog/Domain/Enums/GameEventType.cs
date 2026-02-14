namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Types of game analytics events tracked for trending calculations.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
public enum GameEventType
{
    /// <summary>
    /// Game appeared in search results. Weight: +3
    /// </summary>
    Search = 0,

    /// <summary>
    /// Game detail page viewed. Weight: +1
    /// </summary>
    View = 1,

    /// <summary>
    /// Game added to user library. Weight: +5
    /// </summary>
    LibraryAdd = 2,

    /// <summary>
    /// Game session played. Weight: +10
    /// </summary>
    Play = 3
}
