using Api.Infrastructure.AzureSdk;
using Api.Tests.Constants;
using Azure.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Infrastructure.AzureSdk;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class AzureConfigurationSourcesTests
{
    private static IConfiguration Settings(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddAzureConfigurationSources_NoEndpoints_AddsNothing(string? value)
    {
        // ConfigurationBuilder (non ConfigurationManager): Add registra la sorgente senza costruirla,
        // quindi il test ispeziona Sources senza aprire alcuna connessione.
        var builder = new ConfigurationBuilder();
        var settings = Settings(("AZURE_APPCONFIG_CONNECTION_STRING", value), ("AZURE_APPCONFIG_ENDPOINT", value), ("AZURE_KEYVAULT_URI", value));

        builder.AddAzureConfigurationSources(settings);

        builder.Sources.Should().BeEmpty();
    }

    [Fact]
    public void AddAzureConfigurationSources_AppConfigConnectionString_AddsOneSource()
    {
        var builder = new ConfigurationBuilder();

        builder.AddAzureConfigurationSources(Settings(("AZURE_APPCONFIG_CONNECTION_STRING", "Endpoint=http://127.0.0.1:9;Id=i;Secret=c2VjcmV0")));

        builder.Sources.Should().HaveCount(1);
    }

    [Fact]
    public void AddAzureConfigurationSources_KeyVaultUri_AddsOneSource()
    {
        var builder = new ConfigurationBuilder();

        builder.AddAzureConfigurationSources(Settings(("AZURE_KEYVAULT_URI", "https://127.0.0.1:9/"), ("AZURE_KEYVAULT_ANONYMOUS", "true")));

        builder.Sources.Should().HaveCount(1);
    }

    [Fact]
    public void AddAzureConfigurationSources_InvalidEndpoint_Throws()
    {
        var builder = new ConfigurationBuilder();

        var act = () => builder.AddAzureConfigurationSources(Settings(("AZURE_APPCONFIG_ENDPOINT", "not a url")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*AZURE_APPCONFIG_ENDPOINT*");
    }

    [Fact]
    public void StaticTokenCredential_ReturnsNonExpiringToken()
    {
        var credential = new StaticTokenCredential("anonymous");

        var token = credential.GetToken(new TokenRequestContext(["https://x/.default"]), CancellationToken.None);

        token.Token.Should().Be("anonymous");
        token.ExpiresOn.Should().BeAfter(DateTimeOffset.UtcNow.AddYears(1));
    }
}
