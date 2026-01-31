using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// ISSUE #2374 Phase 5: Audit Log Implementation for SharedGameCatalog
///
/// Event handlers for SharedGameCatalog domain events.
/// Automatically creates audit log entries via DomainEventHandlerBase.
///
/// Events Handled (7 total):
/// - SharedGameCreatedEvent (game creation)
/// - SharedGameUpdatedEvent (game updates)
/// - SharedGameArchivedEvent (game archiving)
/// - SharedGameDeletedEvent (permanent deletion)
/// - SharedGameDeleteRequestedEvent (delete request workflow)
/// - GameFaqAddedEvent (FAQ additions)
/// - GameErrataAddedEvent (errata additions)
///
/// Note: SharedGameDocument* events use INotification (not IDomainEvent).
/// They require manual AuditService integration in command handlers if audit trail needed.
///
/// Audit logs visible at:
/// - GET /api/v1/admin/audit-logs?resource=SharedGame*
/// - Timeline UI component (Issue #2374 Phase 2)
/// </summary>
internal sealed class SharedGameCreatedEventHandler : DomainEventHandlerBase<SharedGameCreatedEvent>
{
    public SharedGameCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<SharedGameCreatedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override Task HandleEventAsync(SharedGameCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Audit log created automatically by base class
        // No additional business logic needed for audit trail
        return Task.CompletedTask;
    }

    protected override Guid? GetUserId(SharedGameCreatedEvent domainEvent)
        => domainEvent.CreatedBy;

    protected override Dictionary<string, object?>? GetAuditMetadata(SharedGameCreatedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
        };
}

internal sealed class SharedGameUpdatedEventHandler : DomainEventHandlerBase<SharedGameUpdatedEvent>
{
    public SharedGameUpdatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<SharedGameUpdatedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override Task HandleEventAsync(SharedGameUpdatedEvent domainEvent, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected override Guid? GetUserId(SharedGameUpdatedEvent domainEvent)
        => domainEvent.ModifiedBy;

    protected override Dictionary<string, object?>? GetAuditMetadata(SharedGameUpdatedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
        };
}

internal sealed class SharedGameArchivedEventHandler : DomainEventHandlerBase<SharedGameArchivedEvent>
{
    public SharedGameArchivedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<SharedGameArchivedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override Task HandleEventAsync(SharedGameArchivedEvent domainEvent, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected override Guid? GetUserId(SharedGameArchivedEvent domainEvent)
        => domainEvent.ArchivedBy;

    protected override Dictionary<string, object?>? GetAuditMetadata(SharedGameArchivedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
        };
}

internal sealed class SharedGameDeletedEventHandler : DomainEventHandlerBase<SharedGameDeletedEvent>
{
    public SharedGameDeletedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<SharedGameDeletedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override Task HandleEventAsync(SharedGameDeletedEvent domainEvent, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected override Guid? GetUserId(SharedGameDeletedEvent domainEvent)
        => domainEvent.DeletedBy;

    protected override Dictionary<string, object?>? GetAuditMetadata(SharedGameDeletedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
        };
}

internal sealed class SharedGameDeleteRequestedEventHandler : DomainEventHandlerBase<SharedGameDeleteRequestedEvent>
{
    public SharedGameDeleteRequestedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<SharedGameDeleteRequestedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override Task HandleEventAsync(SharedGameDeleteRequestedEvent domainEvent, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected override Guid? GetUserId(SharedGameDeleteRequestedEvent domainEvent)
        => domainEvent.RequestedBy;

    protected override Dictionary<string, object?>? GetAuditMetadata(SharedGameDeleteRequestedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
        };
}

// NOTE: SharedGameDocument events (Added, Activated, Removed) use INotification not IDomainEvent
// They cannot extend DomainEventHandlerBase. Audit logging for document operations
// can be added via manual AuditService integration in command handlers if needed.

internal sealed class GameFaqAddedEventHandler : DomainEventHandlerBase<GameFaqAddedEvent>
{
    public GameFaqAddedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<GameFaqAddedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override Task HandleEventAsync(GameFaqAddedEvent domainEvent, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected override Dictionary<string, object?>? GetAuditMetadata(GameFaqAddedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
            ["FaqId"] = domainEvent.FaqId,
            ["Question"] = domainEvent.Question,
        };
}

internal sealed class GameErrataAddedEventHandler : DomainEventHandlerBase<GameErrataAddedEvent>
{
    public GameErrataAddedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<GameErrataAddedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override Task HandleEventAsync(GameErrataAddedEvent domainEvent, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected override Dictionary<string, object?>? GetAuditMetadata(GameErrataAddedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
            ["ErrataId"] = domainEvent.ErrataId,
            ["Description"] = domainEvent.Description,
        };
}
