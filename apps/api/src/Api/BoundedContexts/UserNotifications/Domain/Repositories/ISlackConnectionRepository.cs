using Api.BoundedContexts.UserNotifications.Domain.Aggregates;

namespace Api.BoundedContexts.UserNotifications.Domain.Repositories;

internal interface ISlackConnectionRepository
{
    Task AddAsync(SlackConnection connection, CancellationToken ct = default);
    Task UpdateAsync(SlackConnection connection, CancellationToken ct = default);
    Task<SlackConnection?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<SlackConnection?> GetBySlackUserIdAsync(string slackUserId, CancellationToken ct = default);
    Task<SlackConnection?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Batch-fetches active Slack connections for multiple users.
    /// Returns a dictionary keyed by UserId for O(1) lookup.
    /// </summary>
    Task<Dictionary<Guid, SlackConnection>> GetActiveByUserIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken ct = default);

    Task<int> GetActiveConnectionCountAsync(CancellationToken ct = default);
}
