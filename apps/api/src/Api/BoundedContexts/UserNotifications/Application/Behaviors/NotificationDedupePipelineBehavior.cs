using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.UserNotifications.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that swallows DB unique-constraint violations (PostgreSQL
/// SQLSTATE 23505) scoped to the <c>UX_notifications_user_source_event_id</c> index.
///
/// <para>
/// Problem (ADR-068 + ADR-072 follow-up): <c>NotificationDispatcher.AddAsync</c> performs
/// a pre-check via <c>ExistsBySourceEventIdAsync</c> (#1937 / CF-1) and then stages the
/// notification entity. Under race conditions (two handlers concurrently dispatching the
/// same SourceEventId), the second <c>SaveChangesAsync</c> may still hit the partial
/// unique index, throwing <see cref="DbUpdateException"/> wrapping
/// <see cref="PostgresException"/> SQLSTATE 23505. This rollback propagates to the caller
/// — but the underlying intent (idempotent dispatch on a (UserId, SourceEventId) pair) is
/// already satisfied by the existing notification row created by the first handler.
/// </para>
///
/// <para>
/// Decision (ADR-068 Option A / ADR-072 Option C): swallow this specific exception
/// (matched by SQLSTATE + ConstraintName) and return <c>default!</c> for the originating
/// request. Any other DbUpdateException (different constraint, different SQLSTATE) is
/// re-thrown unmodified, preserving observability for real bugs.
/// </para>
///
/// <para>
/// Scope: applies to every MediatR request. Behaviour is a no-op unless the inner
/// exception matches the precise constraint name, so unrelated handlers pay only the cost
/// of one try/catch entering the call site (negligible — JIT removes the try/catch from
/// the hot path when no exception is thrown).
/// </para>
/// </summary>
/// <typeparam name="TRequest">MediatR request type</typeparam>
/// <typeparam name="TResponse">MediatR response type</typeparam>
internal sealed class NotificationDedupePipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Constraint name of the partial unique index on (UserId, SourceEventId) for the
    /// <c>notifications</c> table. See
    /// <c>Api.Infrastructure.EntityConfigurations.UserNotifications.NotificationEntityConfiguration</c>.
    /// </summary>
    internal const string NotificationDedupConstraintName = "UX_notifications_user_source_event_id";

    private readonly ILogger<NotificationDedupePipelineBehavior<TRequest, TResponse>> _logger;

    public NotificationDedupePipelineBehavior(
        ILogger<NotificationDedupePipelineBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsNotificationDedupViolation(ex))
        {
            _logger.LogInformation(
                ex,
                "Swallowing notification dedup violation for request {RequestType}: " +
                "{ConstraintName} fired on a race; notification already persisted by concurrent dispatch.",
                typeof(TRequest).Name,
                NotificationDedupConstraintName);
            return default!;
        }
    }

    /// <summary>
    /// Returns true if the <see cref="DbUpdateException"/> wraps a PostgreSQL
    /// <c>unique_violation</c> (SQLSTATE 23505) on the notification dedup constraint.
    /// Every other constraint or SQLSTATE is treated as a real bug and re-thrown.
    /// </summary>
    private static bool IsNotificationDedupViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505" } pe
        && string.Equals(pe.ConstraintName, NotificationDedupConstraintName, StringComparison.Ordinal);
}
