using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.DI;

/// <summary>
/// #3435 (SP1) regression: <c>RunImageRegionSeedBatchCommandHandler</c> injects
/// <see cref="IRawHiResExtractor"/> UNCONDITIONALLY (the admin seed-batch endpoint). If the
/// registration ever moves back inside <c>RegisterUnstructuredExtractor</c> (Orchestrator/Unstructured
/// only) — an easy mistake since it sits next to those calls — the endpoint would 500 on MediatR/DI
/// activation under Docnet/SmolDocling. This mirrors the #3269 <see cref="IPdfExtractorHealthProbe"/>
/// regression guard: the raw-hi_res extractor must resolve under EVERY provider.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3435")]
public sealed class RawHiResExtractorRegistrationTests
{
    [Theory]
    [InlineData("Orchestrator")]
    [InlineData("Unstructured")]
    [InlineData("SmolDocling")]
    [InlineData("Docnet")]
    [InlineData("")] // unknown/unset → code default "Orchestrator" via the ?? fallback path
    public void AddDocumentProcessingContext_ResolvesRawHiResExtractor_ForEveryProvider(string provider)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient(); // IHttpClientFactory: the extractor's only hard ctor dependency

        var settings = new Dictionary<string, string>
        {
            ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
            ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = "http://test:8002",
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

        // Act
        services.AddDocumentProcessingContext(configuration);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        // Assert: resolves AND constructs the ctor under every provider.
        var extractor = scope.ServiceProvider.GetRequiredService<IRawHiResExtractor>();
        extractor.Should().BeOfType<UnstructuredPdfTextExtractor>(
            "the seed-image-regions-batch endpoint injects IRawHiResExtractor regardless of the selected provider");
    }

    /// <summary>
    /// Issue #3570: the hi_res budget covers the whole request. At 300s it left no margin — on
    /// staging the seed batch lost descent, terraforming-mars and 7-wonders (the table-heavy
    /// rulebooks) at 302s under concurrent load, while 7-wonders measured 221s in isolation. Three
    /// such aborts dead-letter a PDF out of the VLM pipeline for good, so the default must keep
    /// headroom over the measured cost.
    /// </summary>
    [Fact]
    public void HiResClient_DefaultTimeout_KeepsHeadroomOverMeasuredCost()
    {
        using var serviceProvider = BuildProviderWithoutTimeoutOverride();

        var client = serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(UnstructuredPdfTextExtractor.HiResClientName);

        client.Timeout.Should().Be(TimeSpan.FromSeconds(900));
    }

    [Fact]
    public void HiResClient_Timeout_IsConfigurable()
    {
        using var serviceProvider = BuildProviderWithoutTimeoutOverride(hiResTimeoutSeconds: 1200);

        var client = serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(UnstructuredPdfTextExtractor.HiResClientName);

        client.Timeout.Should().Be(TimeSpan.FromSeconds(1200));
    }

    private static ServiceProvider BuildProviderWithoutTimeoutOverride(int? hiResTimeoutSeconds = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();

        var settings = new Dictionary<string, string>
        {
            ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
            ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = "http://test:8002",
            ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
            ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
            ["PdfProcessing:Quality:MinCharsPerPage"] = "500"
        };
        if (hiResTimeoutSeconds is not null)
        {
            settings["PdfProcessing:Extractor:Unstructured:HiResTimeoutSeconds"] =
                hiResTimeoutSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings!).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<ITextChunkingService, TextChunkingService>();

        services.AddDocumentProcessingContext(configuration);
        return services.BuildServiceProvider();
    }
}
