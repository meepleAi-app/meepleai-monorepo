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
}
