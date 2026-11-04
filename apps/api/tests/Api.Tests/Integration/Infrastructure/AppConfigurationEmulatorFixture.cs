using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Api.Tests.Integration.Infrastructure;

public sealed class AppConfigurationEmulatorFixture : EmulatorFixtureBase
{
    public const string Image = "mcr.microsoft.com/azure-app-configuration/app-configuration-emulator:1.2.0";
    public const int Port = 8483;
    public const string AccessKeyId = "azure-local-dev";
    public const string AccessKeySecret = "YXp1cmUtbG9jYWwtZGV2LXNlY3JldC0wMTIzNDU2Nzg5YWJjZGVm";

    private IContainer? _container;

    public string Endpoint { get; private set; } = string.Empty;
    public string ConnectionString => $"Endpoint={Endpoint};Id={AccessKeyId};Secret={AccessKeySecret}";

    protected override string EmulatorName => "App Configuration emulator";

    protected override async Task StartAsync()
    {
        _container = new ContainerBuilder()
            .WithImage(Image)
            .WithEnvironment("Tenant__AnonymousAuthEnabled", "true")
            .WithEnvironment("Tenant__HmacSha256Enabled", "true")
            .WithEnvironment("Tenant__AccessKeys__0__Id", AccessKeyId)
            .WithEnvironment("Tenant__AccessKeys__0__Name", "primary")
            .WithEnvironment("Tenant__AccessKeys__0__Secret", AccessKeySecret)
            .WithEnvironment("Tenant__AccessKeys__0__ReadOnly", "false")
            .WithEnvironment("Authentication__Anonymous__AnonymousUserRole", "Owner")
            .WithEnvironment("Hosting__IPAddress", "0.0.0.0")
            .WithPortBinding(Port, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(Port)))
            .Build();
        await _container.StartAsync();
        Endpoint = $"http://127.0.0.1:{_container.GetMappedPublicPort(Port)}";
    }

    protected override async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
