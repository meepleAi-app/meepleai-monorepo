using Api.Tests.Infrastructure;
using Testcontainers.Azurite;

namespace Api.Tests.Integration.DocumentProcessing;

public sealed class AzuriteFixture : EmulatorFixtureBase
{
    private AzuriteContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;
    public string PublicEndpoint { get; private set; } = string.Empty;

    protected override string EmulatorName => "Azurite";

    protected override async Task StartAsync()
    {
        // --skipApiVersionCheck: l'SDK azure-storage-blobs invia un x-ms-version piu' recente di quanto Azurite 3.37.0 conosca.
        _container = new AzuriteBuilder()
            .WithImage(TestcontainersConfiguration.AzuriteImage)
            .WithCommand("--skipApiVersionCheck", "--loose")
            .Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        PublicEndpoint = $"http://127.0.0.1:{_container.GetMappedPublicPort(TestcontainersConfiguration.AzuriteBlobPort)}";
    }

    protected override async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
