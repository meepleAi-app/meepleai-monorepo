using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Api.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// Tells a failure of the infrastructure (the database is slow, unreachable, restarting) from a failure of the
/// data (a value the column refuses, a row the model rejects). #4839.
/// </summary>
/// <remarks>
/// The answer decides who retries. An event handler that fails because of the infrastructure should let the
/// failure go up: the outbox retries the row with a backoff, and by then the database is back. A failure of the
/// data would fail the same way every time, and rethrowing it only stops the other handlers of the event and
/// parks the row in <c>Failed</c> after <c>MaxAttempts</c>.
/// </remarks>
internal static class PersistenceFailure
{
    /// <summary>
    /// <c>true</c> when <paramref name="ex"/>, or anything it wraps, is a timeout, an exhausted retry of the
    /// execution strategy (<c>EnableRetryOnFailure</c> gave up on a transient failure), or an Npgsql failure that
    /// Npgsql itself calls transient (connection lost, deadlock, server shutting down, resources exhausted).
    /// </summary>
    public static bool IsInfrastructure(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is TimeoutException or RetryLimitExceededException or NpgsqlException { IsTransient: true })
                return true;
        }

        return false;
    }
}
