using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Extensions;

/// <summary>
/// Shared query helpers for SessionTracking that avoid copy-paste across handlers.
/// </summary>
public static class SessionTrackingDbExtensions
{
    /// <summary>
    /// Resolves the GameNightEvent ID for a given session, if any.
    /// Returns null if the session is not linked to a GameNight.
    /// </summary>
    public static async Task<Guid?> ResolveGameNightIdAsync(
        this MeepleAiDbContext db,
        Guid sessionId,
        CancellationToken ct = default)
    {
        return await db.GameNightSessions
            .Where(gns => gns.SessionId == sessionId)
            .Select(gns => (Guid?)gns.GameNightEventId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
