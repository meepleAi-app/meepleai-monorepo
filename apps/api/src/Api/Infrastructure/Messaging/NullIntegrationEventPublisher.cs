using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Events;

namespace Api.Infrastructure.Messaging;

/// <summary>Profili senza bus: l'evento viene solo loggato a Debug.</summary>
internal sealed class NullIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ILogger<NullIntegrationEventPublisher> _logger;

    public NullIntegrationEventPublisher(ILogger<NullIntegrationEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken) where TEvent : IntegrationEventBase
    {
        _logger.LogDebug("Integration event {EventType} {EventId} not published: no bus configured", typeof(TEvent).Name, integrationEvent.EventId);
        return Task.CompletedTask;
    }
}
