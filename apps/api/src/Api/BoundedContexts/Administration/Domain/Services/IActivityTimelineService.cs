using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Aggregates activity events from multiple bounded contexts
/// into a chronological user timeline (Issue #3973, #3923).
/// Sources: UserLibrary (game_added), Sessions (session_completed),
/// KnowledgeBase (chat_saved), Wishlist (wishlist_added).
/// </summary>
internal interface IActivityTimelineService
{
    /// <summary>
    /// Gets recent activities for a user, aggregated from all sources.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="limit">Maximum events to return (default: 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chronologically ordered activity events (DESC).</returns>
    Task<IReadOnlyList<ActivityEvent>> GetRecentActivitiesAsync(
        Guid userId,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets filtered, searched, and paginated activities for a user (Issue #3923).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="types">Optional event type filter (e.g., game_added, session_completed).</param>
    /// <param name="searchTerm">Optional text search on game name/title.</param>
    /// <param name="dateFrom">Optional start date filter (inclusive).</param>
    /// <param name="dateTo">Optional end date filter (inclusive).</param>
    /// <param name="skip">Number of events to skip (pagination).</param>
    /// <param name="take">Number of events to take (pagination).</param>
    /// <param name="order">Sort direction (ascending or descending by timestamp).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated result with total count.</returns>
    Task<(IReadOnlyList<ActivityEvent> Events, int TotalCount)> GetFilteredActivitiesAsync(
        Guid userId,
        string[]? types = null,
        string? searchTerm = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int skip = 0,
        int take = 20,
        SortDirection order = SortDirection.Descending,
        CancellationToken cancellationToken = default);
}
