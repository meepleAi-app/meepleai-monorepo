using Api.Infrastructure.Messaging;
using Api.SharedKernel.Application.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Infrastructure.Messaging;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class MessagingServiceExtensionsTests
{
    private static ServiceProvider Build(params (string Key, string? Value)[] pairs)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationEventPublishing(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void CreateClientOptions_BoundsRetryAndTimeoutSoABusOutageCostsSeconds()
    {
        var options = MessagingServiceExtensions.CreateClientOptions();

        options.RetryOptions.MaxRetries.Should().Be(1);
        options.RetryOptions.TryTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.TransportType.Should().Be(Azure.Messaging.ServiceBus.ServiceBusTransportType.AmqpTcp);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoBus_RegistersNullPublisher(string? value)
    {
        using var sp = Build(("AZURE_SERVICEBUS_CONNECTION_STRING", value), ("AZURE_SERVICEBUS_NAMESPACE", value));

        sp.GetRequiredService<IIntegrationEventPublisher>().Should().BeOfType<NullIntegrationEventPublisher>();
    }

    [Fact]
    public async Task ConnectionString_RegistersServiceBusPublisher()
    {
        // await using: ServiceBusClient/Sender sono solo IAsyncDisposable. La connessione AMQP è lazy: risolvere il publisher non apre socket.
        await using var sp = Build(("AZURE_SERVICEBUS_CONNECTION_STRING", "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"));

        sp.GetRequiredService<IIntegrationEventPublisher>().Should().BeOfType<ServiceBusIntegrationEventPublisher>();
    }

    [Fact]
    public async Task Namespace_RegistersServiceBusPublisherWithIdentity()
    {
        await using var sp = Build(("AZURE_SERVICEBUS_NAMESPACE", "meepleai-sb.servicebus.windows.net"));

        sp.GetRequiredService<IIntegrationEventPublisher>().Should().BeOfType<ServiceBusIntegrationEventPublisher>();
    }
}
