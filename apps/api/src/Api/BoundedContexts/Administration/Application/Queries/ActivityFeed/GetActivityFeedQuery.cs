using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.ActivityFeed;

/// <summary>
/// Query that returns the cross-entity activity feed for a specific user.
/// Reads from <c>domain_event_logs</c>, filtered by <see cref="UserId"/>,
/// with a 90-day retention window, ordered by <c>LoggedAt DESC</c>.
/// BE-3 #1590 Task 8.
/// </summary>
/// <param name="UserId">The user whose events are fetched (caller-scoped).</param>
/// <param name="Limit">Maximum number of items to return. Validated: 1..100, default 20.</param>
/// <param name="Since">Optional upper-bound cursor: only events logged before this timestamp.</param>
internal sealed record GetActivityFeedQuery(
    Guid UserId,
    int Limit = 20,
    DateTime? Since = null
) : IQuery<GetActivityFeedResult>;

/// <summary>Result envelope returned by <see cref="GetActivityFeedQuery"/>.</summary>
/// <param name="Items">Activity items ordered by LoggedAt DESC.</param>
/// <param name="Count">Count of items in this page (equals Items.Count).</param>
internal sealed record GetActivityFeedResult(
    List<ActivityItemDto> Items,
    int Count
);
