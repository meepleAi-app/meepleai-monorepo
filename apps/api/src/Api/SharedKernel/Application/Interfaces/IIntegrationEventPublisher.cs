using Api.SharedKernel.Domain.Events;

namespace Api.SharedKernel.Application.Interfaces;

/// <summary>
/// Pubblica un integration event verso un sistema esterno al processo (spec azure-local §4.3). I domain event
/// restano su MediatR/outbox; questa porta serve ai consumer fuori dall'API (Azure Functions).
/// </summary>
internal interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken) where TEvent : IntegrationEventBase;
}
