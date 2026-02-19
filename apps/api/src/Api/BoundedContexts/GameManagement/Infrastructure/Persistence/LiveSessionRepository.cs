using System.Collections.Concurrent;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// In-memory repository for live game sessions.
/// LiveGameSession is not EF-persisted (modelBuilder.Ignore) since sessions are transient.
/// Issue #4749: CQRS commands/queries for live sessions.
/// </summary>
internal sealed class LiveSessionRepository : ILiveSessionRepository
{
    private readonly ConcurrentDictionary<Guid, LiveGameSession> _sessions = new();

    public Task<LiveGameSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(id, out var session);
        return Task.FromResult(session);
    }

    public Task<LiveGameSession?> GetByCodeAsync(string sessionCode, CancellationToken cancellationToken = default)
    {
        var session = _sessions.Values.FirstOrDefault(s =>
            string.Equals(s.SessionCode, sessionCode, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<LiveGameSession>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var activeSessions = _sessions.Values
            .Where(s => s.CreatedByUserId == userId && s.IsActive)
            .OrderByDescending(s => s.UpdatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<LiveGameSession>>(activeSessions);
    }

    public Task AddAsync(LiveGameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_sessions.TryAdd(session.Id, session))
            throw new InvalidOperationException($"Session {session.Id} already exists");

        return Task.CompletedTask;
    }

    public Task UpdateAsync(LiveGameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_sessions.ContainsKey(id));
    }
}
