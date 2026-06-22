using Api.Infrastructure;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.SharedKernel.Application.EventHandlers;

/// <summary>
/// Base class for domain event handlers that provides consistent logging + exception bubbling.
/// All domain event handlers should inherit from this class.
///
/// Issue #1534 (2026-06): audit logging is no longer performed by this base class. It is now
/// centralized in <see cref="DomainEventAuditHandler{TEvent}"/>, an open-generic notification
/// handler registered alongside subclass handlers. This collapses the dual write paths
/// (legacy <c>DomainEventHandlerBase.CreateAuditLogAsync</c> writing directly to <c>audit_logs</c>
/// + S1 <c>AuditLoggingBehavior</c> writing to <c>audit_outbox</c>) into a single canonical path:
/// <c>audit_outbox → AuditOutboxProcessor → audit_logs</c>. The <see cref="GetUserId"/> and
/// <see cref="GetAuditMetadata"/> virtual hooks are retained for subclass use (e.g. enriching
/// integration events) but are no longer invoked by <see cref="Handle"/>.
/// </summary>
/// <typeparam name="TEvent">The type of domain event to handle</typeparam>
internal abstract class DomainEventHandlerBase<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent
{
    private readonly MeepleAiDbContext _dbContext;
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEventHandlerBase{TEvent}"/> class.
    /// </summary>
    protected DomainEventHandlerBase(
        MeepleAiDbContext dbContext,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
    }

    /// <summary>
    /// Exposes the bound <see cref="MeepleAiDbContext"/> for derived handlers that need to
    /// query or mutate state while reacting to the domain event.
    /// </summary>
    protected MeepleAiDbContext DbContext => _dbContext;

    /// <summary>
    /// Handles the domain event notification.
    /// Auditing is performed separately by <see cref="DomainEventAuditHandler{TEvent}"/>.
    /// </summary>
    public async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation(
                "Handling domain event {EventType} with ID {EventId} occurred at {OccurredAt}",
                typeof(TEvent).Name,
                notification.EventId,
                notification.OccurredAt);

            await HandleEventAsync(notification, cancellationToken).ConfigureAwait(false);

            Logger.LogInformation(
                "Successfully handled domain event {EventType} with ID {EventId}",
                typeof(TEvent).Name,
                notification.EventId);
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // DOMAIN EVENT PATTERN: Log event handling failures before propagating.
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Error handling domain event {EventType} with ID {EventId}: {Error}",
                typeof(TEvent).Name,
                notification.EventId,
                ex.Message);
            throw;
        }
#pragma warning restore S2139
    }

    /// <summary>
    /// Handles the specific domain event logic.
    /// Override this method in derived classes to implement event-specific handling.
    /// </summary>
    protected abstract Task HandleEventAsync(TEvent domainEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the user ID associated with the event, if any.
    ///
    /// ⚠️ DEAD HOOK (issue #1534, 2026-06-01): no longer invoked by the base <see cref="Handle"/>
    /// flow after the audit-path collapse. Audit user-id resolution is performed via
    /// convention-based reflection in <see cref="DomainEventAuditHandler{TEvent}"/>'s
    /// <c>ActorPropertyNames</c> list. Existing subclass overrides in the 65 handler classes
    /// are compiled but never called; they will be removed in a follow-up cleanup PR. New
    /// handlers should NOT override this method — add the property name to
    /// <c>DomainEventAuditHandler.ActorPropertyNames</c> instead.
    /// </summary>
    protected virtual Guid? GetUserId(TEvent domainEvent) => null;

    /// <summary>
    /// Gets additional metadata for audit logging.
    ///
    /// ⚠️ DEAD HOOK (issue #1534, 2026-06-01): same status as <see cref="GetUserId"/> —
    /// no longer invoked by the base <see cref="Handle"/> flow. <see cref="DomainEventAuditHandler{TEvent}"/>
    /// serializes the whole event into the payload's <c>Details</c> field via <c>JsonSerializer</c>,
    /// so all event properties are captured automatically without per-handler metadata override.
    /// Existing subclass overrides will be removed in a follow-up cleanup PR.
    /// </summary>
    protected virtual Dictionary<string, object?>? GetAuditMetadata(TEvent domainEvent) => null;
}
