using System.Net;
using System.Text;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.DI;

/// <summary>
/// DC-2 (#3419): the hi_res routing relies on <see cref="IExtractionStrategySelector"/> being
/// registered <b>scoped</b> so that <c>PdfProcessingPipelineService</c> (setter) and
/// <see cref="UnstructuredPdfTextExtractor"/> (reader) — both resolved within the same per-PDF scope
/// via the orchestrator's constructor graph — share ONE instance. A refactor to Transient/Singleton
/// would silently break the routing (or leak strategy across PDFs), so pin the lifetime + semantics.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3419")]
public sealed class ExtractionStrategySelectorRegistrationTests
{
    private static IServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();

        var settings = new Dictionary<string, string>
        {
            ["PdfProcessing:Extractor:Provider"] = "Orchestrator",
            ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
            ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = "http://test:8002",
            ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
            ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
            ["PdfProcessing:Quality:MinCharsPerPage"] = "500"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings!).Build();
        services.AddSingleton<IConfiguration>(configuration);
        // ITextChunkingService is an EnhancedPdfProcessingOrchestrator dependency the context expects
        // to be present (mirrors ExtractorHealthProbeRegistrationTests).
        services.AddScoped<ITextChunkingService, TextChunkingService>();

        services.AddDocumentProcessingContext(configuration);
        return services;
    }

    [Fact]
    public void AddDocumentProcessingContext_RegistersSelector_AsScoped()
    {
        var services = BuildServices();

        var descriptor = services.Single(d => d.ServiceType == typeof(IExtractionStrategySelector));

        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
        descriptor.ImplementationType.Should().Be(typeof(ExtractionStrategySelector));
    }

    [Fact]
    public void Selector_IsSharedWithinScope_ButDistinctAcrossScopes()
    {
        using var provider = BuildServices().BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        var a = scope1.ServiceProvider.GetRequiredService<IExtractionStrategySelector>();
        var b = scope1.ServiceProvider.GetRequiredService<IExtractionStrategySelector>();

        using var scope2 = provider.CreateScope();
        var c = scope2.ServiceProvider.GetRequiredService<IExtractionStrategySelector>();

        // Same scope ⇒ the pipeline's set is visible to the same-scope extractor.
        a.Should().BeSameAs(b);
        a.Current = ExtractionStrategy.HiRes;
        b.Current.Should().Be(ExtractionStrategy.HiRes);

        // Different scope (a different PDF's job) ⇒ no strategy bleed; starts at the safe default.
        c.Should().NotBeSameAs(a);
        c.Current.Should().Be(ExtractionStrategy.Fast);
    }

    /// <summary>
    /// End-to-end DI proof: the keyed <see cref="UnstructuredPdfTextExtractor"/> resolved from a scope
    /// reads the <see cref="IExtractionStrategySelector"/> set on that SAME scope — the exact guarantee
    /// PdfProcessingPipelineService relies on (it sets the selector, the extractor built in its scope
    /// graph reads it). Guards against a future refactor that resolves the extractor from a child scope.
    /// </summary>
    [Fact]
    public async Task KeyedUnstructuredExtractor_ReadsSelectorSetOnItsScope()
    {
        string? capturedStrategy = null;
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                if (req.Content is MultipartFormDataContent multipart)
                {
                    foreach (var part in multipart)
                    {
                        if (string.Equals(
                                part.Headers.ContentDisposition?.Name?.Trim('"'),
                                "strategy",
                                StringComparison.Ordinal))
                        {
                            capturedStrategy = await part.ReadAsStringAsync(ct).ConfigureAwait(false);
                        }
                    }
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"text\":\"\",\"chunks\":[],\"quality_score\":0.9,\"page_count\":1}")
                };
            });

        var services = BuildServices();
        // Swap the real socket handler for the capturing mock on the same named client (BaseAddress
        // + Polly from AddDocumentProcessingContext are preserved).
        services.AddHttpClient("UnstructuredService").ConfigurePrimaryHttpMessageHandler(() => handler.Object);
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        // The pipeline would do this; here we set it directly on the scope's selector instance.
        scope.ServiceProvider.GetRequiredService<IExtractionStrategySelector>().Current = ExtractionStrategy.HiRes;

        var extractor = scope.ServiceProvider.GetRequiredKeyedService<IPdfTextExtractor>(
            DocumentProcessingServiceExtensions.PdfExtractorKeys.Unstructured);

        await extractor.ExtractPagedTextAsync(
            new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\ntest")),
            cancellationToken: TestContext.Current.CancellationToken);

        capturedStrategy.Should().Be("hi_res");
    }
}
