using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Application;
using Api.Services;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.SharedKernel.Application.EventHandlers;

/// <summary>
/// Open-generic notification handler that writes a single audit_outbox row for every
/// <see cref="IDomainEvent"/> published through MediatR. Issue #1534: collapses the dual
/// audit write paths (legacy <see cref="DomainEventHandlerBase{TEvent}"/> direct audit_logs write
/// + <see cref="BoundedContexts.Administration.Application.Behaviors.AuditLoggingBehavior{TRequest,TResponse}"/>
/// outbox enqueue) into a single canonical path: AuditOutbox → AuditOutboxProcessor → AuditLogEntity.
///
/// Registered as an open generic in DI:
/// <c>services.AddTransient(typeof(INotificationHandler&lt;&gt;), typeof(DomainEventAuditHandler&lt;&gt;));</c>
/// .NET DI honours the <c>where TEvent : IDomainEvent</c> constraint, so notifications that are
/// not <see cref="IDomainEvent"/> (e.g. integration events implementing only <see cref="INotification"/>)
/// will not resolve this handler — preventing unrelated cross-context broadcasts from being audited.
///
/// Resilience: audit failures are logged and swallowed (mirrors <see cref="AuditService.EnqueueAuditAsync"/>
/// best-effort semantics) — auditing must never break business event dispatch.
/// </summary>
internal sealed class DomainEventAuditHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent
{
    private static readonly JsonSerializerOptions DetailsJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AuditService _auditService;
    private readonly ILogger<DomainEventAuditHandler<TEvent>> _logger;

    public DomainEventAuditHandler(
        AuditService auditService,
        ILogger<DomainEventAuditHandler<TEvent>> logger)
    {
        ArgumentNullException.ThrowIfNull(auditService);
        _auditService = auditService;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var payload = BuildPayload(notification);
            await _auditService.EnqueueAuditAsync(payload, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Resilience: audit failures must never break event handling
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(
                ex,
                "Failed to enqueue audit for domain event {EventType} with ID {EventId}",
                typeof(TEvent).Name,
                notification.EventId);
        }
    }

    private static AuditOutboxPayload BuildPayload(TEvent notification)
    {
        return new AuditOutboxPayload
        {
            Action = $"DomainEvent.{typeof(TEvent).Name}",
            Resource = typeof(TEvent).Name,
            ResourceId = notification.EventId.ToString(),
            UserId = TryExtractUserId(notification)?.ToString(),
            Result = "Success",
            Details = JsonSerializer.Serialize(notification, notification.GetType(), DetailsJsonOptions),
            Timestamp = new DateTimeOffset(DateTime.SpecifyKind(notification.OccurredAt, DateTimeKind.Utc)),
            RequestType = nameof(IDomainEvent),
        };
    }

    /// <summary>
    /// Convention-based UserId extraction: looks for a public <c>UserId</c> property of type
    /// <see cref="Guid"/> (or nullable Guid) on the event type. Returns <c>null</c> when absent —
    /// system-scoped events (no user actor) are valid and remain auditable without a user id.
    /// </summary>
    private static Guid? TryExtractUserId(TEvent notification)
    {
        var property = typeof(TEvent).GetProperty("UserId");
        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(notification);
        return value switch
        {
            Guid id => id,
            null => null,
            _ => null,
        };
    }
}
