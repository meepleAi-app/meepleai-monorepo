using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Builds a per-session winner-name resolver from a cross-BC SessionTracking participant read,
/// scoped to a night's sessions — Issue #2702 (shared by the summary + share-token handlers).
/// A stray/foreign participant id fails closed to null rather than resolving a wrong name.
/// </summary>
internal static class GameNightSummaryWinnerResolver
{
    public static async Task<Func<GameNightSession, string?>> BuildAsync(
        MeepleAiDbContext db, GameNightEvent night, CancellationToken cancellationToken)
    {
        var sessionIds = night.Sessions.Select(s => s.SessionId).ToList();
        var participants = sessionIds.Count == 0
            ? new List<ParticipantRef>()
            : await db.SessionTrackingParticipants.AsNoTracking()
                .Where(p => sessionIds.Contains(p.SessionId))
                .Select(p => new ParticipantRef(p.Id, p.SessionId, p.DisplayName))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        return session =>
            session.WinnerId is { } winnerId
                ? participants
                    .Where(p => p.SessionId == session.SessionId && p.Id == winnerId)
                    .Select(p => p.DisplayName)
                    .FirstOrDefault()
                : null;
    }

    private sealed record ParticipantRef(Guid Id, Guid SessionId, string DisplayName);
}
