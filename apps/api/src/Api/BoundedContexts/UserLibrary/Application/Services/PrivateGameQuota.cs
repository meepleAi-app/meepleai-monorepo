using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Services;

/// <summary>
/// The tier's private-game quota (#4978, #5020), shared by the two paths that create a private game: manual creation
/// (<c>AddPrivateGameCommandHandler</c>) and the BGG import (<c>ImportBggGameCommandHandler</c>).
/// </summary>
/// <remarks>
/// <para>
/// One rule: <c>TierLimits.MaxPrivateGames</c> counts the private games the owner has, deleted ones excluded, and a
/// creation over it is refused with 402 <c>PRIVATE_GAME_QUOTA_EXCEEDED</c>. The BGG import used a daily Redis counter
/// instead, which a deletion never gave back; starting an improvvisata session consumed it too, without creating a
/// game (decision of 2026-11-18 on #5020).
/// </para>
/// <para>
/// The count and the insert are one atomic step: the owner's quota lock is taken first, inside the transaction, so
/// concurrent requests of one owner count and insert one after the other.
/// </para>
/// <para>
/// The transaction runs under the retrying execution strategy, which can run the delegate again after a transient
/// error — also after a SaveChanges that succeeded and a commit that failed, when the staged rows are already accepted
/// (Unchanged) and a second SaveChanges would write nothing. So every attempt stages the creation anew, with fresh
/// aggregates, after forgetting what the previous attempt staged (<c>IPrivateGameRepository.ForgetStagedCreations</c>).
/// The count is taken again, under the lock, on every attempt.
/// </para>
/// </remarks>
internal static class PrivateGameQuota
{
    /// <summary>
    /// Stages the creation through <paramref name="stage"/> and saves it if the owner is under
    /// <paramref name="maxPrivateGames"/>, atomically. <paramref name="stage"/> runs once per attempt and must build the
    /// aggregates it adds, not reuse ones built before.
    /// </summary>
    /// <exception cref="PrivateGameQuotaExceededException">The owner already has the maximum.</exception>
    public static async Task CreateWithinQuotaAsync(
        Guid ownerId,
        int maxPrivateGames,
        Func<CancellationToken, Task> stage,
        IPrivateGameRepository privateGames,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(privateGames);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        var attempt = 0;
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (attempt++ > 0)
            {
                privateGames.ForgetStagedCreations();
            }

            await privateGames.LockOwnerQuotaAsync(ownerId, ct).ConfigureAwait(false);

            var owned = await privateGames.CountByOwnerIdAsync(ownerId, ct).ConfigureAwait(false);
            if (owned >= maxPrivateGames)
            {
                logger.LogInformation(
                    "User {UserId} has {Count} private games, the maximum of the tier ({Max}): creation refused",
                    ownerId,
                    owned,
                    maxPrivateGames);

                throw new PrivateGameQuotaExceededException(maxPrivateGames);
            }

            await stage(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
}
