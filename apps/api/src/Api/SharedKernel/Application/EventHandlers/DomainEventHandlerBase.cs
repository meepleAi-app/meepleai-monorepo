using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.SharedKernel.Application.EventHandlers;

/// <summary>
/// Base class for domain event handlers with automatic audit logging.
/// All domain event handlers should inherit from this class to ensure consistent audit trail.
/// </summary>
/// <typeparam name="TEvent">The type of domain event to handle</typeparam>
public abstract class DomainEventHandlerBase<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent
{
    private readonly MeepleAiDbContext _dbContext;
    protected readonly ILogger<DomainEventHandlerBase<TEvent>> Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEventHandlerBase{TEvent}"/> class.
    /// </summary>
    protected DomainEventHandlerBase(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<TEvent>> logger)
    {
        _dbContext = dbContext;
        Logger = logger;
    }

    /// <summary>
    /// Handles the domain event notification.
    /// </summary>
    public async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Log event
            Logger.LogInformation(
                "Handling domain event {EventType} with ID {EventId} occurred at {OccurredAt}",
                typeof(TEvent).Name,
                notification.EventId,
                notification.OccurredAt);

            // Create audit log entry
            await CreateAuditLogAsync(notification, cancellationToken);

            // Execute derived handler logic
            await HandleEventAsync(notification, cancellationToken);

            Logger.LogInformation(
                "Successfully handled domain event {EventType} with ID {EventId}",
                typeof(TEvent).Name,
                notification.EventId);
        }
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
    }

    /// <summary>
    /// Handles the specific domain event logic.
    /// Override this method in derived classes to implement event-specific handling.
    /// </summary>
    protected abstract Task HandleEventAsync(TEvent domainEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the user ID associated with the event, if any.
    /// Override this method in derived classes to extract user ID from event data.
    /// </summary>
    protected virtual Guid? GetUserId(TEvent domainEvent) => null;

    /// <summary>
    /// Gets additional metadata for audit logging.
    /// Override this method in derived classes to provide event-specific metadata.
    /// </summary>
    protected virtual Dictionary<string, object?>? GetAuditMetadata(TEvent domainEvent) => null;

    /// <summary>
    /// Creates an audit log entry for the domain event.
    /// </summary>
    private async Task CreateAuditLogAsync(TEvent domainEvent, CancellationToken cancellationToken)
    {
        var metadata = GetAuditMetadata(domainEvent) ?? new Dictionary<string, object?>();
        metadata["EventId"] = domainEvent.EventId;
        metadata["OccurredAt"] = domainEvent.OccurredAt;

        var auditLog = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            UserId = GetUserId(domainEvent),
            Resource = typeof(TEvent).Name,
            ResourceId = domainEvent.EventId.ToString(),
            Action = $"DomainEvent.{typeof(TEvent).Name}",
            Result = "Success",
            Details = JsonSerializer.Serialize(metadata),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
