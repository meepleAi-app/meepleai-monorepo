using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Runs a read-decide-write on an invitation again when another writer got there first (#4744).
/// </summary>
/// <remarks>
/// <para>
/// The invitation carries the row version PostgreSQL keeps in <c>xmin</c>, so a write that overlaps another is refused
/// with a <see cref="DbUpdateConcurrencyException"/> instead of undoing it. The two commands that decide from the
/// state they read (the guest's answer, the organizer's withdrawal) handle the refusal the only sound way: forget the
/// entity that was about to be written, read the row again and decide again. The second look is the whole point: an
/// answer that finds the invitation withdrawn becomes a 410, a withdrawal that finds it declined becomes a 409 (a
/// declined invitation is history), and the same answer repeated becomes the idempotent no-op it always was.
/// </para>
/// <para>
/// The events the refused attempt collected are dropped with it: the collector keeps them after a failed save (so
/// that a retried save can still publish them), and the next attempt collects its own, so the same answer would be
/// published twice.
/// </para>
/// <para>
/// Bounded: after <see cref="MaxAttempts"/> refusals the caller gets a 409 and asks again, rather than a loop with
/// no end under a writer that never stops.
/// </para>
/// </remarks>
internal static class InvitationWriteRetry
{
    /// <summary>How many times the read-decide-write runs before the caller is told to try again.</summary>
    public const int MaxAttempts = 3;

    public static async Task RunAsync(
        Func<CancellationToken, Task> attempt,
        IDomainEventCollector events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(events);

        var tried = 0;
        while (true)
        {
            tried++;
            try
            {
                await attempt(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // The entity that was about to be written stays tracked as Modified: a second one with the same key
                // would be refused as a duplicate, so it is forgotten before the row is read again.
                foreach (var entry in ex.Entries)
                {
                    entry.State = EntityState.Detached;
                }

                events.Clear();

                if (tried >= MaxAttempts)
                {
                    throw new ConflictException(
                        "The invitation changed while the request was being saved. Try again.", ex);
                }
            }
        }
    }
}
