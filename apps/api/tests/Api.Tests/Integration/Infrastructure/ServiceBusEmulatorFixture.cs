using Api.Tests.Infrastructure;
using Azure.Messaging.ServiceBus;
using Testcontainers.ServiceBus;

namespace Api.Tests.Integration.Infrastructure;

public sealed class ServiceBusEmulatorFixture : EmulatorFixtureBase
{
    private ServiceBusContainer? _container;

    public ServiceBusClient Client { get; private set; } = null!;

    protected override string EmulatorName => "Service Bus emulator";

    protected override async Task StartAsync()
    {
        _container = new ServiceBusBuilder()
            .WithImage(TestcontainersConfiguration.ServiceBusEmulatorImage)
            .WithAcceptLicenseAgreement(true)
            .Build();
        await _container.StartAsync();
        Client = new ServiceBusClient(_container.GetConnectionString());
    }

    protected override async Task StopAsync()
    {
        if (Client is not null)
        {
            await Client.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
