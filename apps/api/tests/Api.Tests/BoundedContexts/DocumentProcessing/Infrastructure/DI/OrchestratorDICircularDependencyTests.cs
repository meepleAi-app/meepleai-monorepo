using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using static Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection.DocumentProcessingServiceExtensions;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.DI;

/// <summary>
/// ISSUE-1174: Tests for orchestrator DI circular dependency fix using keyed services
/// POST-MERGE: Updated to use PdfExtractorKeys constants for compile-time safety
/// </summary>
public class OrchestratorDICircularDependencyTests
{
    [Fact]
    public void AddDocumentProcessingContext_WithOrchestratorProvider_ShouldResolveWithoutCircularDependency()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PdfProcessing:Extractor:Provider"] = "Orchestrator",
                ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
                ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = "http://test:8002",
                ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
                ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
                ["PdfProcessing:Quality:MinCharsPerPage"] = "500"
            }!)
            .Build();

        // Register IConfiguration for domain services
        services.AddSingleton<IConfiguration>(configuration);

        // Act - Should not throw InvalidOperationException (circular dependency)
        services.AddDocumentProcessingContext(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify primary extractor resolves to OrchestratedPdfTextExtractor
        var extractor = serviceProvider.GetRequiredService<IPdfTextExtractor>();
        Assert.NotNull(extractor);
        Assert.IsType<OrchestratedPdfTextExtractor>(extractor);
    }

    [Fact]
    public void AddDocumentProcessingContext_WithOrchestratorProvider_ShouldResolveOrchestratorService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PdfProcessing:Extractor:Provider"] = "Orchestrator",
                ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
                ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = "http://test:8002",
                ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
                ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
                ["PdfProcessing:Quality:MinCharsPerPage"] = "500"
            }!)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddDocumentProcessingContext(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify orchestrator application service can be resolved
        var orchestrator = serviceProvider.GetRequiredService<EnhancedPdfProcessingOrchestrator>();
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void AddDocumentProcessingContext_WithUnstructuredProvider_ShouldResolveCorrectExtractor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PdfProcessing:Extractor:Provider"] = "Unstructured",
                ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
                ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
                ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
                ["PdfProcessing:Quality:MinCharsPerPage"] = "500"
            }!)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddDocumentProcessingContext(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var extractor = serviceProvider.GetRequiredService<IPdfTextExtractor>();
        Assert.NotNull(extractor);
        Assert.IsType<UnstructuredPdfTextExtractor>(extractor);
    }

    [Fact]
    public void AddDocumentProcessingContext_WithSmolDoclingProvider_ShouldResolveCorrectExtractor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PdfProcessing:Extractor:Provider"] = "SmolDocling",
                ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = "http://test:8002",
                ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
                ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
                ["PdfProcessing:Quality:MinCharsPerPage"] = "500"
            }!)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddDocumentProcessingContext(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var extractor = serviceProvider.GetRequiredService<IPdfTextExtractor>();
        Assert.NotNull(extractor);
        Assert.IsType<SmolDoclingPdfTextExtractor>(extractor);
    }

    [Fact]
    public void AddDocumentProcessingContext_WithDocnetProvider_ShouldResolveCorrectExtractor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PdfProcessing:Extractor:Provider"] = "Docnet",
                ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
                ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
                ["PdfProcessing:Quality:MinCharsPerPage"] = "500"
            }!)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddDocumentProcessingContext(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var extractor = serviceProvider.GetRequiredService<IPdfTextExtractor>();
        Assert.NotNull(extractor);
        Assert.IsType<DocnetPdfTextExtractor>(extractor);
    }

    [Fact]
    public void AddDocumentProcessingContext_WithOrchestratorProvider_ShouldResolveKeyedExtractors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["PdfProcessing:Extractor:Provider"] = "Orchestrator",
                ["PdfProcessing:Extractor:Unstructured:ApiUrl"] = "http://test:8001",
                ["PdfProcessing:Extractor:SmolDocling:ApiUrl"] = "http://test:8002",
                ["PdfProcessing:MaxFileSizeBytes"] = "104857600",
                ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
                ["PdfProcessing:Quality:MinCharsPerPage"] = "500"
            }!)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddDocumentProcessingContext(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify keyed extractors can be resolved using constants
        var unstructuredExtractor = serviceProvider.GetRequiredKeyedService<IPdfTextExtractor>(PdfExtractorKeys.Unstructured);
        Assert.NotNull(unstructuredExtractor);
        Assert.IsType<UnstructuredPdfTextExtractor>(unstructuredExtractor);

        var smolDoclingExtractor = serviceProvider.GetRequiredKeyedService<IPdfTextExtractor>(PdfExtractorKeys.SmolDocling);
        Assert.NotNull(smolDoclingExtractor);
        Assert.IsType<SmolDoclingPdfTextExtractor>(smolDoclingExtractor);

        var docnetExtractor = serviceProvider.GetRequiredKeyedService<IPdfTextExtractor>(PdfExtractorKeys.Docnet);
        Assert.NotNull(docnetExtractor);
        Assert.IsType<DocnetPdfTextExtractor>(docnetExtractor);
    }
}

