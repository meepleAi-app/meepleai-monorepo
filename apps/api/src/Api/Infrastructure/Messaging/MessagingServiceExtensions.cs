using Api.SharedKernel.Application.Interfaces;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace Api.Infrastructure.Messaging;

internal static class MessagingServiceExtensions
{
    public const string ConnectionStringKey = "AZURE_SERVICEBUS_CONNECTION_STRING";
    public const string NamespaceKey = "AZURE_SERVICEBUS_NAMESPACE";
    public const string QueueKey = "AZURE_SERVICEBUS_QUEUE";
    public const string DefaultQueue = "pdf-uploaded";

    /// <summary>
    /// Il publish gira dentro la transazione per-batch dell'outbox dei domain event: un bus giù deve costare
    /// secondi, non minuti (default SDK: 3 retry x 60 s), altrimenti blocca tutti gli altri eventi del batch.
    /// </summary>
    public static ServiceBusClientOptions CreateClientOptions() => new()
    {
        TransportType = ServiceBusTransportType.AmqpTcp,
        RetryOptions = new ServiceBusRetryOptions { MaxRetries = 1, TryTimeout = TimeSpan.FromSeconds(5) },
    };

    /// <summary>
    /// Service Bus parla AMQP, non HTTP: il client <c>AzureSdk</c> pinnato non si applica. L'endpoint viene da
    /// configurazione statica (stessa esenzione di OpenRouter/Resend nel gate di egress). Con la connection
    /// string (emulatore o SAS) o il namespace FQDN (Azure vero, identità Entra) registra il publisher Service Bus;
    /// altrimenti il publisher nullo.
    /// </summary>
    public static IServiceCollection AddIntegrationEventPublishing(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration[ConnectionStringKey];
        var fullyQualifiedNamespace = configuration[NamespaceKey];
        var hasConnectionString = !string.IsNullOrWhiteSpace(connectionString);
        var hasNamespace = !string.IsNullOrWhiteSpace(fullyQualifiedNamespace);
        if (!hasConnectionString && !hasNamespace)
        {
            services.AddSingleton<IIntegrationEventPublisher, NullIntegrationEventPublisher>();
            return services;
        }

        var queue = configuration[QueueKey] is { Length: > 0 } q ? q : DefaultQueue;
        var clientOptions = CreateClientOptions();
        services.AddSingleton(_ => hasConnectionString
            ? new ServiceBusClient(connectionString, clientOptions)
            : new ServiceBusClient(fullyQualifiedNamespace!.Trim(), new DefaultAzureCredential(), clientOptions));
        services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateSender(queue));
        services.AddSingleton<IIntegrationEventPublisher, ServiceBusIntegrationEventPublisher>();
        return services;
    }
}
