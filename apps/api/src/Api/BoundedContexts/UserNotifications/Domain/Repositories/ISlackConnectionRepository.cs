using Api.BoundedContexts.UserNotifications.Domain.Aggregates;

namespace Api.BoundedContexts.UserNotifications.Domain.Repositories;

internal interface ISlackConnectionRepository
{
    Task AddAsync(SlackConnection connection, CancellationToken ct = default);
    Task UpdateAsync(SlackConnection connection, CancellationToken ct = default);
    Task<SlackConnection?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<SlackConnection?> GetBySlackUserIdAsync(string slackUserId, CancellationToken ct = default);
    Task<SlackConnection?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetActiveConnectionCountAsync(CancellationToken ct = default);
}
