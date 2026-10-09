using System.Text.Json;
using System.Text.Json.Serialization;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Events;
using Azure.Messaging.ServiceBus;

namespace Api.Infrastructure.Messaging;

internal sealed class ServiceBusIntegrationEventPublisher : IIntegrationEventPublisher
{
    /// <summary>Formato di filo degli integration event: camelCase, enum come stringa. Condiviso con il test di contratto.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusIntegrationEventPublisher> _logger;

    public ServiceBusIntegrationEventPublisher(ServiceBusSender sender, ILogger<ServiceBusIntegrationEventPublisher> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken) where TEvent : IntegrationEventBase
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(integrationEvent, JsonOptions))
        {
            ContentType = "application/json",
            MessageId = integrationEvent.EventId.ToString(),
            Subject = typeof(TEvent).Name,
        };
        message.ApplicationProperties["sourceContext"] = integrationEvent.SourceContext;
        if (integrationEvent is IVersionedContract versioned)
        {
            message.ApplicationProperties["contractVersion"] = versioned.ContractVersion;
        }

        await _sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Published {EventType} {EventId} to {Entity}", typeof(TEvent).Name, integrationEvent.EventId, _sender.EntityPath);
    }
}
