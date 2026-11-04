using System.Net;
using System.Text;
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
/// #4435: the document language reaches <see cref="UnstructuredPdfTextExtractor"/> through the scoped
/// <see cref="IExtractionLanguageSelector"/> (same pattern as the DC-2 strategy selector), and the
/// default set comes from the configuration key <c>PdfProcessing:Extractor:Unstructured:Language</c>
/// through the real options binding of <c>AddDocumentProcessingContext</c>. These tests go through
/// the real DI graph, so a key that is bound but not read — the state before #4435 — shows up here.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4435")]
public sealed class ExtractionLanguageSelectorRegistrationTests
{
    private static IServiceCollection BuildServices(string? configuredLanguage, Func<string?, Task> onLanguage)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();

        var settings = new Dictionary<string, string?>
        {
            ["PdfProcessing:Extractor:Provider"] = "Orchestrator",
            ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
            ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = "http://test:8002",
            ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
            ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
            ["PdfProcessing:Quality:MinCharsPerPage"] = "500",
        };
        if (configuredLanguage is not null)
        {
            settings["PdfProcessing:Extractor:Unstructured:Language"] = configuredLanguage;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<ITextChunkingService, TextChunkingService>();
        services.AddDocumentProcessingContext(configuration);

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
                        if (string.Equals(part.Headers.ContentDisposition?.Name?.Trim('"'), "language", StringComparison.Ordinal))
                        {
                            await onLanguage(await part.ReadAsStringAsync(ct).ConfigureAwait(false)).ConfigureAwait(false);
                        }
                    }
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"text\":\"\",\"chunks\":[],\"quality_score\":0.9,\"page_count\":1}")
                };
            });
        services.AddHttpClient("UnstructuredService").ConfigurePrimaryHttpMessageHandler(() => handler.Object);
        return services;
    }

    private static Stream Pdf() => new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\ntest"));

    [Fact]
    public void AddDocumentProcessingContext_RegistersLanguageSelector_AsScoped()
    {
        var services = BuildServices(configuredLanguage: null, _ => Task.CompletedTask);

        var descriptor = services.Single(d => d.ServiceType == typeof(IExtractionLanguageSelector));

        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
        descriptor.ImplementationType.Should().Be(typeof(ExtractionLanguageSelector));
    }

    [Fact]
    public async Task KeyedUnstructuredExtractor_ReadsLanguageSetOnItsScope()
    {
        string? sent = null;
        using var provider = BuildServices("eng+ita+deu", l => { sent = l; return Task.CompletedTask; })
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IExtractionLanguageSelector>().Current =
            new DocumentLanguageHint(LanguageOverride: "de", DetectedLanguage: null, DetectedConfidence: null);
        var extractor = scope.ServiceProvider.GetRequiredKeyedService<IPdfTextExtractor>(
            DocumentProcessingServiceExtensions.PdfExtractorKeys.Unstructured);

        await extractor.ExtractPagedTextAsync(Pdf(), cancellationToken: TestContext.Current.CancellationToken);

        sent.Should().Be("deu+eng+ita");
    }

    [Theory]
    [InlineData("eng+ita+deu")]
    [InlineData("eng+ita")]
    public async Task KeyedUnstructuredExtractor_DocumentWithoutLanguage_SendsTheConfiguredKey(string configured)
    {
        string? sent = null;
        using var provider = BuildServices(configured, l => { sent = l; return Task.CompletedTask; })
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        var extractor = scope.ServiceProvider.GetRequiredKeyedService<IPdfTextExtractor>(
            DocumentProcessingServiceExtensions.PdfExtractorKeys.Unstructured);

        await extractor.ExtractPagedTextAsync(Pdf(), cancellationToken: TestContext.Current.CancellationToken);

        sent.Should().Be(configured);
    }

    [Fact]
    public async Task HealthProbe_SendsTheConfiguredKey()
    {
        string? sent = null;
        using var provider = BuildServices("eng+ita", l => { sent = l; return Task.CompletedTask; })
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPdfExtractorHealthProbe>()
            .IsHealthyAsync(TestContext.Current.CancellationToken);

        sent.Should().Be("eng+ita");
    }
}
