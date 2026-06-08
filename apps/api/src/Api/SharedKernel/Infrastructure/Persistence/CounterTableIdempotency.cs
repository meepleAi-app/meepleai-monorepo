using Microsoft.EntityFrameworkCore;

namespace Api.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// Helper for detecting PostgreSQL unique-constraint violations on counter / append
/// tables guarded by the CF-2 (#1938) idempotency partial index. Centralises the
/// SQL-state check so that any service inserting a row with <c>source_event_id</c>
/// can wrap its <see cref="DbContext.SaveChangesAsync(System.Threading.CancellationToken)"/>
/// call uniformly:
///
/// <code>
/// try { await _db.SaveChangesAsync(ct); }
/// catch (DbUpdateException ex) when (CounterTableIdempotency.IsUniqueViolation(ex))
/// {
///     _db.Entry(entity).State = EntityState.Detached;
///     return; // idempotent skip
/// }
/// </code>
///
/// Mirrors the inline pattern already used in <c>EmailOutboxService.EnqueueAsync</c>
/// (UserNotifications) and <c>NotificationRepository.AddAndCommitAsync</c> (CF-1).
/// </summary>
internal static class CounterTableIdempotency
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="ex"/> wraps a PostgreSQL
    /// <c>23505 unique_violation</c> error — the signal that a concurrent caller
    /// (another pod, an outbox retry, a rolled-back-then-retried tx) already
    /// persisted a row keyed by the same <c>source_event_id</c>.
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException pgEx
            && string.Equals(pgEx.SqlState, "23505", System.StringComparison.Ordinal);
    }
}
