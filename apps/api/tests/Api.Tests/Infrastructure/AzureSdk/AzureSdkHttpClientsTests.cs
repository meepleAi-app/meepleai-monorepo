using Api.Infrastructure.AzureSdk;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Infrastructure.AzureSdk;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class AzureSdkHttpClientsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    private static ServiceProvider Build(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.AddLogging();
        services.AddAzureSdkHttpClients(config);
        return services.BuildServiceProvider();
    }

    // The egress gate keys registrations by string literal ("AzureSdk"/"AzureSdkLocal"), while
    // CreateTransport looks clients up by constant: they must stay equal or the pin is silently lost.
    [Fact]
    public void Constants_MatchTheRegisteredLiterals()
    {
        AzureSdkHttpClients.PinnedName.Should().Be("AzureSdk");
        AzureSdkHttpClients.LocalName.Should().Be("AzureSdkLocal");
    }

    [Fact]
    public void IsLocalProfile_IsCaseInsensitiveAndFalseWhenAbsentOrEmpty()
    {
        AzureSdkHttpClients.IsLocalProfile(Config(("MEEPLEAI_PROFILE", "Azure-Local"))).Should().BeTrue();
        AzureSdkHttpClients.IsLocalProfile(Config(("MEEPLEAI_PROFILE", ""))).Should().BeFalse();
        AzureSdkHttpClients.IsLocalProfile(Config()).Should().BeFalse();
    }

    [Fact]
    public void AllowInsecureTls_OnlyInLocalProfileWithExplicitTrue()
    {
        AzureSdkHttpClients.AllowInsecureTls(Config(("MEEPLEAI_PROFILE", "azure-local"), ("AZURE_SDK_ALLOW_INSECURE_TLS", "true"))).Should().BeTrue();
        AzureSdkHttpClients.AllowInsecureTls(Config(("MEEPLEAI_PROFILE", "azure-exam"), ("AZURE_SDK_ALLOW_INSECURE_TLS", "true"))).Should().BeFalse();
        AzureSdkHttpClients.AllowInsecureTls(Config(("MEEPLEAI_PROFILE", "azure-local"), ("AZURE_SDK_ALLOW_INSECURE_TLS", ""))).Should().BeFalse();
    }

    [Fact]
    public void ReadAllowedHostSuffixes_TrimsAndDropsBlanks()
    {
        var config = Config(
            ("Azure:Sdk:AllowedHostSuffixes:0", " .blob.core.windows.net "),
            ("Azure:Sdk:AllowedHostSuffixes:1", ""),
            ("Azure:Sdk:AllowedHostSuffixes:2", ".vault.azure.net"));

        AzureSdkHttpClients.ReadAllowedHostSuffixes(config).Should().Equal(".blob.core.windows.net", ".vault.azure.net");
    }

    [Fact]
    public void CreateTransport_LocalProfile_UsesLocalClientEvenWithoutSuffixes()
    {
        using var sp = Build(Config(("MEEPLEAI_PROFILE", "azure-local")));

        AzureSdkHttpClients.CreateTransport(sp).Should().NotBeNull();
        sp.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(AzureSdkHttpClients.LocalName)
            .HttpMessageHandlerBuilderActions.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateTransport_AzureProfileWithoutSuffixes_Throws()
    {
        using var sp = Build(Config(("MEEPLEAI_PROFILE", "azure-exam")));

        var act = () => AzureSdkHttpClients.CreateTransport(sp);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Azure:Sdk:AllowedHostSuffixes*");
    }

    [Fact]
    public void CreateTransport_AzureProfileWithSuffixes_UsesPinnedClient()
    {
        using var sp = Build(Config(("MEEPLEAI_PROFILE", "azure-exam"), ("Azure:Sdk:AllowedHostSuffixes:0", ".blob.core.windows.net")));

        AzureSdkHttpClients.CreateTransport(sp).Should().NotBeNull();
        sp.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(AzureSdkHttpClients.PinnedName)
            .HttpMessageHandlerBuilderActions.Should().NotBeEmpty();
    }
}
