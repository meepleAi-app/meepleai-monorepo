using Api.BoundedContexts.Administration.Application.DTOs;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Aggregates activity events from multiple bounded contexts
/// into a chronological user timeline (Issue #3973).
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
}
