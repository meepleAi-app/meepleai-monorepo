using Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.DI;

/// <summary>
/// #4319: la pulizia dei caricamenti a pezzi è un servizio periodico, e gli host di integrazione li tolgono tutti:
/// nessun test d'integrazione si accorgerebbe che manca la registrazione, o che il contenitore non sa costruirlo. Un
/// servizio che non si costruisce, d'altra parte, fa fallire l'avvio dell'API.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4319")]
public sealed class AbandonedChunkedUploadCleanupRegistrationTests
{
    [Fact]
    public void AddDocumentProcessingContext_RegistersTheCleanupAsAHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfProcessing:Extractor:Provider"] = "Orchestrator",
                ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
                ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = "http://test:8002",
                ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
                ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
                ["PdfProcessing:Quality:MinCharsPerPage"] = "500",
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<ITextChunkingService, TextChunkingService>();

        services.AddDocumentProcessingContext(configuration);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(AbandonedChunkedUploadCleanupService));
    }

    [Fact]
    public void TheContainer_CanBuildTheCleanup_WithItsOptionalBasePathLeftToTheDefault()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHostedService<AbandonedChunkedUploadCleanupService>();
        using var provider = services.BuildServiceProvider();

        var hosted = provider.GetServices<IHostedService>().ToList();

        hosted.Should().ContainSingle().Which.Should().BeOfType<AbandonedChunkedUploadCleanupService>();
    }
}
