using Api.BoundedContexts.GameManagement.Application.IntegrationEvents;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Handles the GameCreatedEvent domain event.
/// Creates audit log entry automatically via base class.
/// Publishes integration event for cross-context communication.
/// </summary>
public sealed class GameCreatedEventHandler : DomainEventHandlerBase<GameCreatedEvent>
{
    private readonly IMediator _mediator;

    public GameCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<GameCreatedEvent>> logger,
        IMediator mediator)
        : base(dbContext, logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    protected override async Task HandleEventAsync(GameCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class

        // Publish integration event for cross-context communication
        var integrationEvent = new GameCreatedIntegrationEvent(
            gameId: domainEvent.GameId,
            gameName: domainEvent.Name,
            bggId: domainEvent.BggId
        );

        await _mediator.Publish(integrationEvent, cancellationToken).ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
            ["Name"] = domainEvent.Name,
            ["BggId"] = domainEvent.BggId,
            ["Action"] = "GameCreated"
        };
    }
}
