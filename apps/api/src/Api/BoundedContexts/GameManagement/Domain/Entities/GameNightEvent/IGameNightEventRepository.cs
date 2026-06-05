using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// Repository interface for GameNightEvent aggregate.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
internal interface IGameNightEventRepository : IRepository<GameNightEvent, Guid>
{
    /// <summary>
    /// Gets upcoming published game nights ordered by scheduled date.
    /// </summary>
    Task<IReadOnlyList<GameNightEvent>> GetUpcomingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets game nights where the user is organizer or invited.
    /// </summary>
    Task<IReadOnlyList<GameNightEvent>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets published events with ScheduledAt between from and to, for reminder scheduling.
    /// </summary>
    Task<IReadOnlyList<GameNightEvent>> GetEventsNeedingReminderAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the GameNightEvent aggregate containing a linked Session
    /// (matched via <c>GameNightSession.SessionId</c>). Returns null if the Session
    /// is standalone (not linked to any GameNight).
    /// </summary>
    /// <remarks>
    /// Used by <c>SessionStartedHandler</c> (Asse A WP2 T3, invariante #15) to locate
    /// the parent game night when a Session transitions to live mode.
    /// </remarks>
    Task<GameNightEvent?> FindByLinkedSessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
