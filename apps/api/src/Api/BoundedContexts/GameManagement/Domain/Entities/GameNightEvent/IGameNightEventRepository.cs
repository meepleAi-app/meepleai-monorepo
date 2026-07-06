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
    /// Gets the most recently completed game nights, DESC by ScheduledAt.
    /// F20 #1974 (audit 2026-06-07): wires the dashboard "Recenti" slot
    /// (Asse C P2 follow-up). Also surfaces published events whose
    /// scheduled time has passed, since the BE does not auto-mark
    /// Published → Completed on a scheduler.
    /// </summary>
    /// <param name="limit">Maximum rows to return (capped at 50 in the
    /// implementation for safety).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<GameNightEvent>> GetCompletedAsync(int limit, CancellationToken cancellationToken = default);

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

    // ── Candidate voting (approval model) — Issue #2700 ──────────────────────
    // Votes are immutable append/remove children persisted with tracked Add/Remove.
    // They deliberately bypass the aggregate's detached-Update full-remap: EF's
    // Update() marks a brand-new PK-set child as Modified → 0-row UPDATE. Loading the
    // event (which Includes Votes) still preserves existing votes across an event edit.

    /// <summary>Inserts a single approval vote (tracked Add → INSERT).</summary>
    Task AddVoteAsync(GameNightVote vote, CancellationToken cancellationToken = default);

    /// <summary>Removes a single approval vote by its id (tracked delete). No-op when absent.</summary>
    Task RemoveVoteAsync(Guid voteId, CancellationToken cancellationToken = default);

    /// <summary>Persists the organiser's resolved tie-break winner on the event row.</summary>
    Task SetVotingWinnerAsync(Guid eventId, Guid? winnerGameId, CancellationToken cancellationToken = default);
}
