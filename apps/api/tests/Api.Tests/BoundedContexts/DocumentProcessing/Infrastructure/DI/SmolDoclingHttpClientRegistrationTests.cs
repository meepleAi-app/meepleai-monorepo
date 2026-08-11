using Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.DI;

/// <summary>
/// #3569 regression: <c>GetPdfPageImageQueryHandler</c> (import-wizard page preview) resolves the
/// <c>SmolDoclingService</c> named client UNCONDITIONALLY, but the client used to be configured only
/// for the SmolDocling/Orchestrator providers. Under the default Docnet provider
/// <c>CreateClient</c> still returns a client — one with no <c>BaseAddress</c> — so the relative POST
/// threw "An invalid request URI was provided" and the endpoint answered 500.
/// Mirrors the #3435 <c>RawHiResExtractorRegistrationTests</c> and the #3269 health-probe guard.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3569")]
public sealed class SmolDoclingHttpClientRegistrationTests
{
    private const string SmolDoclingUrl = "http://test:8002";

    private static ServiceProvider BuildProvider(string provider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();

        var settings = new Dictionary<string, string>
        {
            ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
            ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = SmolDoclingUrl,
            ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
            ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
            ["PdfProcessing:Quality:MinCharsPerPage"] = "500"
        };
        if (!string.IsNullOrEmpty(provider))
        {
            settings["PdfProcessing:Extractor:Provider"] = provider;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings!).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<ITextChunkingService, TextChunkingService>();

        services.AddDocumentProcessingContext(configuration);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("Orchestrator")]
    [InlineData("Unstructured")]
    [InlineData("SmolDocling")]
    [InlineData("Docnet")]
    [InlineData("")] // unknown/unset → code default "Orchestrator"
    public void SmolDoclingNamedClient_HasBaseAddress_ForEveryProvider(string provider)
    {
        using var serviceProvider = BuildProvider(provider);

        var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("SmolDoclingService");

        client.BaseAddress.Should().NotBeNull(
            "GetPdfPageImageQueryHandler posts a relative URI to this client under every extractor provider");
        client.BaseAddress!.ToString().Should().StartWith(SmolDoclingUrl);
    }

    [Fact]
    public void SmolDoclingNamedClient_IsConfiguredOnce_UnderTheSmolDoclingProvider()
    {
        // The unconditional registration runs before the provider branch, which also used to register
        // the client. AddHttpClient accumulates configuration per name, so a leftover second call
        // would stack a duplicate retry policy — assert the timeout still reflects a single
        // application of the configured value rather than silently diverging.
        using var serviceProvider = BuildProvider("SmolDocling");

        var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("SmolDoclingService");

        client.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        client.BaseAddress!.ToString().Should().StartWith(SmolDoclingUrl);
    }
}
