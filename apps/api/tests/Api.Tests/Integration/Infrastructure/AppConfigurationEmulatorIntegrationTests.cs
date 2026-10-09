using System.Text;
using Api.Infrastructure.AzureSdk;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Feature", "AzureAppConfiguration")]
public sealed class AppConfigurationEmulatorIntegrationTests : IClassFixture<AppConfigurationEmulatorFixture>
{
    private readonly AppConfigurationEmulatorFixture _emulator;

    public AppConfigurationEmulatorIntegrationTests(AppConfigurationEmulatorFixture emulator)
    {
        _emulator = emulator;
    }

    private async Task SeedAsync(string key, string value)
    {
        using var http = new HttpClient();
        using var body = new StringContent($"{{\"value\":\"{value}\"}}", Encoding.UTF8, "application/vnd.microsoft.appconfig.kv+json");
        var put = await http.PutAsync(new Uri($"{_emulator.Endpoint}/kv/{Uri.EscapeDataString(key)}?api-version=2023-11-01"), body);
        put.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task HmacConnectionString_ReadsSeededKey()
    {
        _emulator.SkipIfUnavailable();
        await SeedAsync("Azure:Profile:Greeting", "from-emulator");
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AzureConfigurationSources.AppConfigConnectionStringKey] = _emulator.ConnectionString })
            .Build();

        var configuration = new ConfigurationBuilder().AddAzureConfigurationSources(settings).Build();

        configuration["Azure:Profile:Greeting"].Should().Be("from-emulator");
    }

    [Fact]
    public async Task HmacConnectionString_WinsOverEnvironmentStyleValue()
    {
        _emulator.SkipIfUnavailable();
        await SeedAsync("Azure:Profile:Precedence", "from-emulator");
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AzureConfigurationSources.AppConfigConnectionStringKey] = _emulator.ConnectionString })
            .Build();

        // Stesso ordine di Program.cs: prima le "variabili d'ambiente" (in-memory), poi le sorgenti Azure.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Azure:Profile:Precedence"] = "from-env" })
            .AddAzureConfigurationSources(settings)
            .Build();

        configuration["Azure:Profile:Precedence"].Should().Be("from-emulator");
    }
}
