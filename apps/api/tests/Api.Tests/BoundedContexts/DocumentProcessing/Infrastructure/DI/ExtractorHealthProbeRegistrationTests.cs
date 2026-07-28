using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.DI;

/// <summary>
/// #3269 regression: <see cref="Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue.BulkReindexReadyCommandHandler"/>
/// injects <see cref="IPdfExtractorHealthProbe"/> UNCONDITIONALLY (the C2 gate that refuses a bulk
/// re-index against a stale extractor). The registration used to live only inside
/// <c>RegisterUnstructuredExtractor</c> (Orchestrator/Unstructured providers), so on
/// Docnet/SmolDocling/fallback the <c>POST /admin/queue/reindex-ready</c> endpoint threw
/// <see cref="System.InvalidOperationException"/> ("Unable to resolve service for type
/// IPdfExtractorHealthProbe") → HTTP 500. The probe must now resolve under EVERY provider.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3269")]
public sealed class ExtractorHealthProbeRegistrationTests
{
    [Theory]
    [InlineData("Orchestrator")]
    [InlineData("Unstructured")]
    [InlineData("SmolDocling")]
    [InlineData("Docnet")]
    [InlineData("")] // unknown/unset → code default "Orchestrator", exercised via the ?? fallback path
    public void AddDocumentProcessingContext_ResolvesHealthProbe_ForEveryProvider(string provider)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient(); // IHttpClientFactory: the probe's only ctor dependency

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

        // Assert: resolves AND constructs (GetRequiredService activates the ctor) under every provider.
        var probe = serviceProvider.GetRequiredService<IPdfExtractorHealthProbe>();
        probe.Should().BeOfType<UnstructuredExtractorHealthProbe>(
            "the bulk re-index C2 gate injects IPdfExtractorHealthProbe regardless of the selected provider");
    }
}
