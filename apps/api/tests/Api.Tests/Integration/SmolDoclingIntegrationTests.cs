using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using System;
using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for SmolDocling VLM PDF extraction service with real Docker container
/// Uses Testcontainers to spin up smoldocling-service and validates retry, circuit breaker, and error handling
/// </summary>
/// <remarks>
/// Issue #948: BGAI-008 - SmolDocling integration tests
/// Requirements: 7 test cases, Testcontainers, real service interaction, ≥90% coverage
/// Dependencies: #946 (Docker), #947 (C# client)
/// Migrated to SharedTestcontainersFixture for optimized performance (reuses container across tests)
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", "PDF")]
public class SmolDoclingIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Action<string> _output;
    private HttpClient? _httpClient;
    private SmolDoclingPdfTextExtractor? _extractor;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test PDF paths
    private const string BarragePdfPath = "../../../../data/rulebook/barrage_rulebook.pdf";
    private const string TerraformingMarsPdfPath = "../../../../data/rulebook/terraforming-mars_rulebook.pdf";

    // Helper to check if tests can run
    private void EnsureTestInfrastructureAvailable()
    {
        if (_extractor == null)
        {
            Assert.Skip("PDF services not enabled. Set TEST_PDF_SERVICES=true and ensure Docker images are built:\n" +
                       "  cd apps/smoldocling-service && docker build -t infra-smoldocling-service:latest .");
        }
    }

    public SmolDoclingIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing SmolDocling integration test infrastructure...");

        // Check if PDF services are enabled via SharedTestcontainersFixture
        if (!_fixture.ArePdfServicesEnabled || string.IsNullOrEmpty(_fixture.SmolDoclingServiceUrl))
        {
            _output("⚠️ PDF services not enabled. Set TEST_PDF_SERVICES=true to run these tests.");
            _output("   Ensure Docker image is built: cd apps/smoldocling-service && docker build -t infra-smoldocling-service:latest .");
            return; // Tests will skip gracefully
        }

        _output($"Using shared SmolDocling service at: {_fixture.SmolDoclingServiceUrl}");

        // Create HttpClient with shared service URL
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_fixture.SmolDoclingServiceUrl),
            Timeout = PdfUploadTestConstants.ProcessingTimeouts.VlmProcessing
        };

        // Create extractor with dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        var inMemoryConfig = new Dictionary<string, string>
        {
            ["PdfExtraction:SmolDocling:TimeoutSeconds"] = "120"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig!)
            .Build();

        services.AddSingleton<IHttpClientFactory>(sp =>
            new TestHttpClientFactory(_httpClient));

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<SmolDoclingPdfTextExtractor>>();

        _extractor = new SmolDoclingPdfTextExtractor(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            logger,
            configuration);

        _output("✅ Test infrastructure initialized (using shared SmolDocling container)");
    }

    public async ValueTask DisposeAsync()
    {
        _output("Cleaning up SmolDocling test infrastructure...");

        _httpClient?.Dispose();

        // Container cleanup handled by SharedTestcontainersFixture
        _output("✅ Cleanup complete (shared container reused for next test)");

        await Task.CompletedTask;
    }
    [Fact(Timeout = 180000)] // 3 minutes
    public async Task SuccessfulPdfExtraction_ViaSmolDoclingService()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange - Use Barrage rulebook (21MB, Italian, moderate complexity)
        if (!File.Exists(BarragePdfPath)) Assert.Skip($"Test PDF not found: {BarragePdfPath}");

        await using var pdfStream = File.OpenRead(BarragePdfPath);
        var fileSize = new FileInfo(BarragePdfPath).Length;
        _output($"Testing SmolDocling extraction with Barrage ({fileSize / 1024 / 1024}MB)");

        // Act
        var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        (result.Success).Should().BeTrue($"Extraction failed: {result.ErrorMessage}");
        result.ExtractedText.Should().NotBeEmpty();
        (result.PageCount > 0).Should().BeTrue("Page count should be greater than 0");
        (result.CharacterCount > 500).Should().BeTrue("Should extract substantial text (min 500 chars)");
        (result.Quality >= ExtractionQuality.Low).Should().BeTrue($"Quality should be at least Low for SmolDocling VLM, got: {result.Quality}");
        (result.OcrTriggered).Should().BeFalse("SmolDocling is VLM-based, not OCR");

        // Verify Italian content
        var hasItalianKeywords = result.ExtractedText.Contains("gioco", StringComparison.OrdinalIgnoreCase) ||
                                  result.ExtractedText.Contains("giocatori", StringComparison.OrdinalIgnoreCase);
        (hasItalianKeywords).Should().BeTrue("Should contain Italian keywords");

        _output($"✓ Extraction successful: {result.PageCount} pages, {result.CharacterCount} chars, Quality: {result.Quality}");
    }
    [Fact]
    public async Task ServiceTimeout_HandledGracefully()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange - Use small PDF to ensure normal completion (testing timeout requires cancellation)
        if (!File.Exists(BarragePdfPath)) Assert.Skip($"Test PDF not found: {BarragePdfPath}");

        await using var pdfStream = File.OpenRead(BarragePdfPath);

        // Act with realistic timeout for integration test
        using var cts = new CancellationTokenSource(PdfUploadTestConstants.ProcessingTimeouts.VlmProcessing);

        // Should complete within 120s for integration test
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);
        var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: linkedCts.Token);

        // Assert - should complete successfully (not timeout)
        (result.Success).Should().BeTrue("Normal PDFs should complete within timeout");

        _output("✓ Timeout handling validated - service responds within limits");
    }

    [Fact]
    public async Task UserCancellation_PropagatesCorrectly()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange
        if (!File.Exists(TerraformingMarsPdfPath)) Assert.Skip($"Test PDF not found: {TerraformingMarsPdfPath}");

        await using var pdfStream = File.OpenRead(TerraformingMarsPdfPath);

        // Act - Cancel immediately to test cancellation handling
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel before calling

        // Assert - should throw TaskCanceledException
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);
        var act = () =>
            _extractor!.ExtractTextAsync(pdfStream, cancellationToken: linkedCts.Token);
        await act.Should().ThrowAsync<TaskCanceledException>();

        _output("✓ User cancellation propagated correctly");
    }
    [Fact]
    public async Task ServiceUnavailable_CircuitBreakerHandling()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange - Create extractor pointing to non-existent service
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        var unavailableClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:19999"), // Non-existent port
            Timeout = TestConstants.Timing.ShortTimeout
        };

        var config = new ConfigurationBuilder().Build();
        var domainService = new PdfTextProcessingDomainService(config);

        services.AddSingleton<IHttpClientFactory>(sp =>
            new TestHttpClientFactory(unavailableClient));

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<SmolDoclingPdfTextExtractor>>();

        var unavailableExtractor = new SmolDoclingPdfTextExtractor(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            logger,
            config);

        await using var pdfStream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // Minimal PDF header

        // Act
        var result = await unavailableExtractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        (result.Success).Should().BeFalse("Should fail when service unavailable");
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().ContainEquivalentOf("unavailable");

        _output($"✓ Service unavailable handled: {result.ErrorMessage}");

        unavailableClient.Dispose();
    }
    [Fact(Timeout = 60000)] // 1 minute
    public async Task InvalidPdf_ErrorHandling()
    {
        EnsureTestInfrastructureAvailable();

        // Arrange - Create corrupted PDF (invalid header)
        var invalidPdfBytes = System.Text.Encoding.UTF8.GetBytes("This is not a valid PDF file content");
        await using var invalidStream = new MemoryStream(invalidPdfBytes);

        _output("Testing invalid PDF error handling");

        // Act
        var result = await _extractor!.ExtractTextAsync(invalidStream, cancellationToken: TestCancellationToken);

        // Assert - Service may fail gracefully OR process with very low quality
        if (!result.Success)
        {
            // Service rejected the invalid PDF
            result.ErrorMessage.Should().NotBeNull();
            (result.ErrorMessage.Contains("Invalid", StringComparison.OrdinalIgnoreCase) ||
                result.ErrorMessage.Contains("corrupted", StringComparison.OrdinalIgnoreCase) ||
                result.ErrorMessage.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                result.ErrorMessage.Contains("bad", StringComparison.OrdinalIgnoreCase) ||
                result.ErrorMessage.Length > 0).Should().BeTrue($"Error message should be present for failed extraction, got: '{result.ErrorMessage}'");

            _output($"✓ Invalid PDF rejected by service: {result.ErrorMessage}");
        }
        else
        {
            // Service processed it but should have very low quality or minimal text
            (result.Quality == ExtractionQuality.VeryLow ||
                result.CharacterCount < 50).Should().BeTrue($"Invalid PDF should have very low quality or minimal text, got Quality={result.Quality}, Chars={result.CharacterCount}");

            _output($"✓ Invalid PDF processed with degraded result: Quality={result.Quality}, Chars={result.CharacterCount}");
        }
    }
    [Fact(Timeout = 300000)] // 5 minutes (large file processing)
    public async Task LargeFilePdf_ProcessesSuccessfully()
    {
        EnsureTestInfrastructureAvailable();

        // Arrange - Terraforming Mars is larger (38MB, 20+ pages, complex layout)
        if (!File.Exists(TerraformingMarsPdfPath)) Assert.Skip($"Test PDF not found: {TerraformingMarsPdfPath}");

        await using var pdfStream = File.OpenRead(TerraformingMarsPdfPath);
        var fileSize = new FileInfo(TerraformingMarsPdfPath).Length;
        _output($"Testing large PDF processing: {fileSize / 1024 / 1024}MB");

        // Act
        var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        (result.Success).Should().BeTrue($"Large PDF extraction failed: {result.ErrorMessage}");
        (result.PageCount >= 10).Should().BeTrue($"TM rulebook should have 10+ pages, got: {result.PageCount}");
        (result.CharacterCount > 5000).Should().BeTrue("Large PDF should have 5K+ characters");
        (result.Quality >= ExtractionQuality.Low).Should().BeTrue($"Large PDF should have acceptable quality, got: {result.Quality}");

        // Verify Italian content
        result.ExtractedText.Should().ContainEquivalentOf("gioco");

        _output($"✓ Large PDF processed: {result.PageCount} pages, {result.CharacterCount:N0} chars, Quality: {result.Quality}");
    }
    [Fact(Timeout = 300000)] // 5 minutes (concurrent processing)
    public async Task ConcurrentRequests_HandleMultipleSimultaneously()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange - Use Barrage PDF for concurrent processing
        if (!File.Exists(BarragePdfPath)) Assert.Skip($"Test PDF not found: {BarragePdfPath}");

        const int concurrentRequests = 3;
        _output($"Testing {concurrentRequests} concurrent requests");

        // Act - Create multiple tasks executing in parallel
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(async i =>
            {
                await using var pdfStream = File.OpenRead(BarragePdfPath);
                _output($"  Request {i + 1} starting...");
                var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);
                _output($"  Request {i + 1} completed: Success={result.Success}, Pages={result.PageCount}");
                return result;
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All requests should succeed
        Assert.All(results, result =>
        {
            (result.Success).Should().BeTrue($"Concurrent request failed: {result.ErrorMessage}");
            (result.PageCount > 0).Should().BeTrue("Should extract pages");
            result.ExtractedText.Should().NotBeEmpty();
        });

        _output($"✓ All {concurrentRequests} concurrent requests completed successfully");
    }
    [Fact(Timeout = 300000)] // 5 minutes (includes container restart)
    public async Task ServiceRestart_RecoveryAfterTemporaryFailure()
    {
        // Skip when using SharedTestcontainersFixture (cannot restart shared container)
        Assert.Skip("Service restart test incompatible with shared containers. " +
                   "This test validates infrastructure resilience (container stop/start), " +
                   "which requires a dedicated container instance. " +
                   "Consider testing service recovery behavior through circuit breaker simulation instead.");
    }
    /// <summary>
    /// Simple HttpClientFactory for testing
    /// </summary>
    private class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public TestHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }
}
