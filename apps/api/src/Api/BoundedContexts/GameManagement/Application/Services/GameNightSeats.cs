using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// The one place a seat at a game night is decided (#4947). Members (RSVP rows on the aggregate) and guests
/// invited by email (another aggregate) share the seats, so two doors — the app and the email link — read
/// the same thing here before an answer becomes <c>Accepted</c>.
/// </summary>
/// <remarks>
/// <para>
/// A seat decision is a read-then-write: count who sits, compare with <c>MaxPlayers</c>, write the answer. Two
/// answers that overlap both read «one seat left» and both write. The row version of the night (ADR-060) does not
/// help: a guest's answer writes the invitation, not the night. So the decision is taken under a transaction-scoped
/// advisory lock per night (<see cref="IGameNightEventRepository.LockSeatsAsync"/>), and the night is read AGAIN
/// under it: the copy read before the lock may already be short of a seat. The lock is held until the transaction
/// commits, so the caller runs inside <c>IUnitOfWork.ExecuteInTransactionAsync</c> (#3636, never
/// <c>BeginTransactionAsync</c>) and takes the lock only when a seat is about to be taken: a decline, a «maybe» or
/// the same answer repeated do not touch it.
/// </para>
/// <para>
/// The invitation the guest writes is not re-read here: it carries its own row version, so a stale copy is refused
/// at save and <see cref="InvitationWriteRetry"/> runs the attempt again, lock included.
/// </para>
/// </remarks>
internal static class GameNightSeats
{
    /// <summary>The 409 code when the table has no seat left (#4844 style: lower snake case).</summary>
    public const string FullCode = "game_night_full";

    public const string FullMessage = "This game night is full and cannot accept more players";

    /// <summary>
    /// Takes the night's seat lock, then reads the night and its accepted email guests under it. Must run inside
    /// a transaction, or the lock is released before the caller decides anything.
    /// </summary>
    /// <exception cref="NotFoundException">The night disappeared between the caller's first read and this one.</exception>
    public static async Task<(GameNightEvent Night, int AcceptedGuests)> ReadUnderLockAsync(
        Guid gameNightId,
        IGameNightEventRepository nights,
        IGameNightInvitationRepository invitations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nights);
        ArgumentNullException.ThrowIfNull(invitations);

        await nights.LockSeatsAsync(gameNightId, cancellationToken).ConfigureAwait(false);

        var night = await nights.GetByIdAsync(gameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", gameNightId.ToString());
        var acceptedGuests = await invitations
            .CountAcceptedByGameNightIdAsync(gameNightId, cancellationToken)
            .ConfigureAwait(false);

        return (night, acceptedGuests);
    }

    /// <summary>The refusal both doors give when the table is full.</summary>
    public static ConflictException Full() => new(FullCode, FullMessage);
}
