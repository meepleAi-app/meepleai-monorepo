using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="ILiveSessionLinkReader"/>: reads
/// <c>LiveGameSession.CorrelatedGameSessionId</c> backwards, from the GameSession to the live
/// session that created it (#4156).
/// </summary>
internal sealed class LiveSessionLinkReader : ILiveSessionLinkReader
{
    private readonly MeepleAiDbContext _dbContext;

    public LiveSessionLinkReader(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetLiveSessionIdsAsync(
        IReadOnlyCollection<Guid> gameSessionIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gameSessionIds);

        if (gameSessionIds.Count == 0)
            return new Dictionary<Guid, Guid>();

        var ids = gameSessionIds as Guid[] ?? gameSessionIds.ToArray();

        var rows = await _dbContext.LiveGameSessions.AsNoTracking()
            .Where(live => live.CorrelatedGameSessionId != null
                           && ids.Contains(live.CorrelatedGameSessionId.Value))
            .Select(live => new { GameSessionId = live.CorrelatedGameSessionId!.Value, live.Id, live.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // The saga correlates one live session to one GameSession. If the data ever carries
        // more than one, the most recently created live session wins, so the link is stable.
        return rows
            .GroupBy(row => row.GameSessionId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(row => row.CreatedAt).ThenBy(row => row.Id).First().Id);
    }
}
