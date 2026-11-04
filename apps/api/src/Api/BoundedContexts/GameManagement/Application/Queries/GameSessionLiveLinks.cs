using Api.BoundedContexts.GameManagement.Application.DTOs;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Fills <see cref="GameSessionDto.LiveSessionId"/> on the reads the frontend uses to list
/// or open a GameSession (#4156). One query for the whole page, not one per row.
/// </summary>
internal static class GameSessionLiveLinks
{
    public static async Task<List<GameSessionDto>> WithLiveSessionIdsAsync(
        this List<GameSessionDto> sessions,
        ILiveSessionLinkReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(reader);

        if (sessions.Count == 0)
            return sessions;

        var links = await reader
            .GetLiveSessionIdsAsync(sessions.Select(s => s.Id).ToList(), cancellationToken)
            .ConfigureAwait(false);

        if (links.Count == 0)
            return sessions;

        return sessions
            .Select(s => links.TryGetValue(s.Id, out var liveSessionId) ? s with { LiveSessionId = liveSessionId } : s)
            .ToList();
    }
}
