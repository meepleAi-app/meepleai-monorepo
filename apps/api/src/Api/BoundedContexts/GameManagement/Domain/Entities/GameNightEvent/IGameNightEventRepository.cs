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
}
