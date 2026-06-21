using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Shared helper that captures a PlayRecord's CURRENT editable details as a new
/// <see cref="PlayRecordVersion"/> snapshot BEFORE the caller mutates the record.
/// Reused by <c>UpdatePlayRecordCommandHandler</c> (Task 2) and
/// <c>RestorePlayRecordVersionCommandHandler</c> (Task 4). #2437-3.
/// </summary>
internal static class PlayRecordVersionSnapshotter
{
    /// <summary>Maximum number of versions retained per record.</summary>
    public const int MaxVersions = 5;

    /// <summary>
    /// Captures the record's current editable details as a new version.
    /// MUST be called BEFORE <c>PlayRecord.UpdateDetails</c> so the snapshot
    /// represents the state the user is about to overwrite (i.e. a restore point).
    /// The caller is responsible for calling <c>IUnitOfWork.SaveChangesAsync</c>
    /// to persist the new version together with the update, and then calling
    /// <c>PruneOldestAsync</c> + another <c>SaveChangesAsync</c> to enforce the cap.
    /// </summary>
    /// <param name="versionRepository">Repository used to read the next version number and add the snapshot.</param>
    /// <param name="record">The PlayRecord about to be mutated.</param>
    /// <param name="userId">User triggering the update (stored on the version for audit).</param>
    /// <param name="timeProvider">Clock abstraction for the snapshot's CreatedAt timestamp.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SnapshotCurrentAsync(
        IPlayRecordVersionRepository versionRepository,
        PlayRecord record,
        Guid userId,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(versionRepository);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var versionNumber = await versionRepository
            .GetNextVersionNumberAsync(record.Id, ct)
            .ConfigureAwait(false);

        await versionRepository.AddAsync(
            new PlayRecordVersion(
                Guid.NewGuid(),
                record.Id,
                versionNumber,
                record.SessionDate,
                record.Notes,
                record.Location,
                timeProvider.GetUtcNow().UtcDateTime,
                userId),
            ct)
            .ConfigureAwait(false);
    }
}
