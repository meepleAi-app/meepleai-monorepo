namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// The addresses that asked to stop receiving game night emails (#4846). Consulted before every send to a
/// guest; written by the one-tap unsubscribe that the guest emails carry.
/// </summary>
internal interface IGameNightGuestEmailOptOutRepository
{
    /// <summary>Whether <paramref name="email"/> is on the list. The address is normalized here, so callers pass it as they have it.</summary>
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Stages the entry; the caller's unit of work writes it.</summary>
    Task AddAsync(GameNightGuestEmailOptOut optOut, CancellationToken cancellationToken = default);
}
