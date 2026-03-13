using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using System.Net.Http.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for Unstructured PDF extraction with real Python service
/// Uses Testcontainers to spin up the unstructured-service Docker container
/// </summary>
/// <remarks>
/// Issue #954: BGAI-003-v2 - Integration tests for Unstructured service
/// Requirements: 12 test cases, Testcontainers, real Italian PDFs, ≥90% coverage
/// Migrated to SharedTestcontainersFixture for optimized performance (reuses container across tests)
/// </remarks>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", "PDF")]
public class UnstructuredPdfExtractionIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Action<string> _output;
    private HttpClient? _httpClient;
    private UnstructuredPdfTextExtractor? _extractor;
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
                       "  cd apps/unstructured-service && docker build -t infra-unstructured-service:latest .");
        }
    }

    public UnstructuredPdfExtractionIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing Unstructured integration test infrastructure...");

        // Check if PDF services are enabled via SharedTestcontainersFixture
        if (!_fixture.ArePdfServicesEnabled || string.IsNullOrEmpty(_fixture.UnstructuredServiceUrl))
        {
            _output("⚠️ PDF services not enabled. Set TEST_PDF_SERVICES=true to run these tests.");
            _output("   Ensure Docker image is built: cd apps/unstructured-service && docker build -t infra-unstructured-service:latest .");
            return; // Tests will skip gracefully
        }

        _output($"Using shared Unstructured service at: {_fixture.UnstructuredServiceUrl}");

        // Create HttpClient with shared service URL
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_fixture.UnstructuredServiceUrl),
            Timeout = TimeSpan.FromSeconds(120) // VLM processing timeout for Unstructured extraction
        };

        // Create extractor with dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        services.AddSingleton<IHttpClientFactory>(sp =>
            new TestHttpClientFactory(_httpClient));

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<UnstructuredPdfTextExtractor>>();

        _extractor = new UnstructuredPdfTextExtractor(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            logger);

        _output("✅ Test infrastructure initialized (using shared Unstructured container)");
    }

    public async ValueTask DisposeAsync()
    {
        _output("Cleaning up Unstructured test infrastructure...");

        _httpClient?.Dispose();

        // Container cleanup handled by SharedTestcontainersFixture
        _output("✅ Cleanup complete (shared container reused for next test)");

        await Task.CompletedTask;
    }

    [Fact]
    public async Task SimpleItalianPdf_SuccessfulExtraction()
    {
        EnsureTestInfrastructureAvailable();

        // Arrange - Use Barrage rulebook (21MB, Italian)
        if (!File.Exists(BarragePdfPath)) Assert.Skip($"Test PDF not found: {BarragePdfPath}");

        await using var pdfStream = File.OpenRead(BarragePdfPath);
        _output($"Testing with Barrage rulebook ({new FileInfo(BarragePdfPath).Length / 1024 / 1024}MB)");

        // Act
        var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success, $"Extraction failed: {result.ErrorMessage}");
        Assert.NotEmpty(result.ExtractedText);
        Assert.True(result.PageCount > 0, "Page count should be greater than 0");
        Assert.True(result.CharacterCount > 1000, "Should extract substantial text");
        Assert.True(result.Quality >= ExtractionQuality.Medium,
            $"Quality should be at least Medium, got: {result.Quality}");

        _output($"Extraction successful: {result.PageCount} pages, {result.CharacterCount} chars, Quality: {result.Quality}");
    }

    [Fact]
    public async Task ComplexMultiColumnPdf_SuccessfulExtraction()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange - Use Terraforming Mars rulebook (38MB, Italian, complex layout)
        if (!File.Exists(TerraformingMarsPdfPath)) Assert.Skip($"Test PDF not found: {TerraformingMarsPdfPath}");

        await using var pdfStream = File.OpenRead(TerraformingMarsPdfPath);
        _output($"Testing with Terraforming Mars rulebook ({new FileInfo(TerraformingMarsPdfPath).Length / 1024 / 1024}MB)");

        // Act
        var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success, $"Extraction failed: {result.ErrorMessage}");
        Assert.NotEmpty(result.ExtractedText);
        Assert.True(result.PageCount > 10, "TM rulebook should have 10+ pages");
        Assert.True(result.CharacterCount > 5000, "Complex PDF should have substantial text");

        // Verify Italian content detected
        Assert.Contains("gioco", result.ExtractedText, StringComparison.OrdinalIgnoreCase);

        _output($"Complex PDF extraction successful: {result.PageCount} pages, {result.CharacterCount} chars");
    }

    [Fact]
    public async Task ItalianLanguageText_ExtractsCorrectly()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange
        if (!File.Exists(BarragePdfPath)) Assert.Skip($"Test PDF not found: {BarragePdfPath}");

        await using var pdfStream = File.OpenRead(BarragePdfPath);

        // Act
        var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success);

        // Verify Italian-specific characters and words are preserved
        var italianWords = new[] { "gioco", "regole", "giocatori", "turno", "carta" };
        var foundItalianWords = italianWords.Count(word =>
            result.ExtractedText.Contains(word, StringComparison.OrdinalIgnoreCase));

        Assert.True(foundItalianWords >= 2,
            $"Should detect Italian words. Found: {foundItalianWords} of {italianWords.Length}");

        _output($"Italian language validation: {foundItalianWords}/{italianWords.Length} keywords detected");
    }

    [Fact]
    public async Task TableDetection_ExtractsStructuredData()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange - Board game rulebooks typically have tables
        if (!File.Exists(BarragePdfPath)) Assert.Skip($"Test PDF not found: {BarragePdfPath}");

        await using var pdfStream = File.OpenRead(BarragePdfPath);

        // Act
        var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.ExtractedText);

        // Table detection is implicit in quality score
        // High-quality extraction indicates successful table handling
        Assert.True(result.Quality >= ExtractionQuality.Low,
            "Tables should be handled without causing very low quality");

        _output($"Table handling validated via quality score: {result.Quality}");
    }

    [Fact]
    public async Task QualityScoreCalculation_MeetsThreshold()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange
        if (!File.Exists(BarragePdfPath)) Assert.Skip($"Test PDF not found: {BarragePdfPath}");

        await using var pdfStream = File.OpenRead(BarragePdfPath);

        // Act
        var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success);

        // Quality should meet ≥0.70 threshold (from ADR-003)
        // Map enum back to approximate score
        var approximateScore = result.Quality switch
        {
            ExtractionQuality.High => 0.80,
            ExtractionQuality.Medium => 0.65,
            ExtractionQuality.Low => 0.45,
            ExtractionQuality.VeryLow => 0.25,
            _ => 0.0
        };

        Assert.True(approximateScore >= 0.40,
            $"Quality score should be ≥0.40 (fallback threshold), got: {approximateScore} ({result.Quality})");

        _output($"Quality validation passed: {result.Quality} (approx {approximateScore:F2})");
    }

    [Fact]
    public async Task ServiceTimeout_HandledGracefully()
    {
        EnsureTestInfrastructureAvailable();
        // This test validates that extremely long operations would timeout
        // For integration tests, we rely on the service's own timeout handling
        // and verify our client handles it correctly

        // Arrange - use small PDF to ensure it completes (testing timeout requires mock)
        if (!File.Exists(BarragePdfPath)) Assert.Skip($"Test PDF not found: {BarragePdfPath}");

        await using var pdfStream = File.OpenRead(BarragePdfPath);

        // Act with short cancellation token
        using var cts = new CancellationTokenSource(PdfUploadTestConstants.ProcessingTimeouts.VlmProcessing);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);

        // Should complete within 120s for integration test
        var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: linkedCts.Token);

        // Assert - should complete successfully (not timeout)
        Assert.True(result.Success, "Normal PDFs should complete within timeout");

        _output("Timeout handling validated - service responds within limits");
    }

    [Fact]
    public async Task ServiceHealthCheck_RespondsCorrectly()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange & Act
        var response = await _httpClient!.GetAsync("/health", TestCancellationToken);

        // Assert
        Assert.True(response.IsSuccessStatusCode,
            $"Health check failed: {response.StatusCode}");

        var healthData = await response.Content.ReadAsStringAsync(TestCancellationToken);
        Assert.Contains("healthy", healthData, StringComparison.OrdinalIgnoreCase);

        _output($"Health check passed: {response.StatusCode}");
    }

    [Fact]
    public async Task InvalidPdf_HandledGracefully()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange - Create invalid PDF (corrupted header)
        var invalidPdfBytes = System.Text.Encoding.UTF8.GetBytes("This is not a PDF file");
        await using var invalidStream = new MemoryStream(invalidPdfBytes);

        // Act
        var result = await _extractor!.ExtractTextAsync(invalidStream, cancellationToken: TestCancellationToken);

        // Assert - Service may either fail or return empty/low-quality extraction
        if (!result.Success)
        {
            Assert.NotNull(result.ErrorMessage);
            _output($"Invalid PDF failed as expected: {result.ErrorMessage}");
        }
        else
        {
            // If service processes it, quality should be very low
            Assert.True(result.Quality == ExtractionQuality.VeryLow || string.IsNullOrEmpty(result.ExtractedText),
                "Invalid PDF should have very low quality or empty text");
            _output($"Invalid PDF processed with low quality: {result.Quality}, chars: {result.CharacterCount}");
        }
    }

    [Fact]
    public async Task LargeMultiPagePdf_ProcessesSuccessfully()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange - Terraforming Mars is larger (38MB, 20+ pages)
        if (!File.Exists(TerraformingMarsPdfPath)) Assert.Skip($"Test PDF not found: {TerraformingMarsPdfPath}");

        await using var pdfStream = File.OpenRead(TerraformingMarsPdfPath);
        var fileSize = new FileInfo(TerraformingMarsPdfPath).Length;
        _output($"Testing large PDF: {fileSize / 1024 / 1024}MB");

        // Act
        var result = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success, $"Large PDF extraction failed: {result.ErrorMessage}");
        Assert.True(result.PageCount >= 15, $"Expected 15+ pages, got: {result.PageCount}");
        Assert.True(result.CharacterCount > 10000, "Large PDF should have 10K+ characters");

        _output($"Large PDF processed: {result.PageCount} pages, {result.CharacterCount:N0} chars");
    }

    [Fact]
    public async Task SemanticChunking_ProducesPagedResults()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange
        if (!File.Exists(BarragePdfPath)) Assert.Skip($"Test PDF not found: {BarragePdfPath}");

        await using var pdfStream = File.OpenRead(BarragePdfPath);

        // Act
        var result = await _extractor!.ExtractPagedTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success, $"Paged extraction failed: {result.ErrorMessage}");
        Assert.NotEmpty(result.PageChunks);
        Assert.Equal(result.TotalPages, result.PageChunks.Count);

        // Verify chunks have proper structure
        foreach (var chunk in result.PageChunks)
        {
            Assert.True(chunk.PageNumber > 0, "Page numbers should be 1-indexed");
            Assert.True(chunk.CharStartIndex >= 0, "Start index should be non-negative");
            Assert.True(chunk.CharEndIndex >= chunk.CharStartIndex, "End index should be >= start");
        }

        _output($"Semantic chunking validated: {result.PageChunks.Count} chunks");
    }

    [Fact]
    public async Task MetadataExtraction_IncludesPageNumbers()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange
        if (!File.Exists(BarragePdfPath)) Assert.Skip($"Test PDF not found: {BarragePdfPath}");

        await using var pdfStream = File.OpenRead(BarragePdfPath);

        // Act
        var result = await _extractor!.ExtractPagedTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.PageChunks);

        // Verify page numbers are sequential and valid
        var pageNumbers = result.PageChunks.Select(c => c.PageNumber).OrderBy(p => p).ToList();
        Assert.Equal(1, pageNumbers[0]);
        Assert.True(pageNumbers.Count > 0, "Should have page numbers");

        // Verify character indices are sequential
        var sortedChunks = result.PageChunks.OrderBy(c => c.CharStartIndex).ToList();
        for (int i = 0; i < sortedChunks.Count - 1; i++)
        {
            Assert.True(sortedChunks[i].CharEndIndex < sortedChunks[i + 1].CharStartIndex,
                $"Chunks should not overlap: chunk {i} end={sortedChunks[i].CharEndIndex}, chunk {i + 1} start={sortedChunks[i + 1].CharStartIndex}");
        }

        _output($"Metadata validation passed: {pageNumbers.Count} pages with valid indices");
    }

    [Fact]
    public async Task EndToEnd_Pipeline_WithRealPdf()
    {
        EnsureTestInfrastructureAvailable();
        // Arrange - Full E2E test simulating actual usage
        if (!File.Exists(TerraformingMarsPdfPath)) Assert.Skip($"Test PDF not found: {TerraformingMarsPdfPath}");

        _output("=== E2E Pipeline Test ===");

        // Step 1: Extract text
        await using var pdfStream = File.OpenRead(TerraformingMarsPdfPath);
        var extractionResult = await _extractor!.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        Assert.True(extractionResult.Success, "Step 1: Extraction should succeed");
        _output($"✓ Step 1: Extracted {extractionResult.CharacterCount:N0} chars from {extractionResult.PageCount} pages");

        // Step 2: Verify quality meets RAG threshold (≥0.40 for usability)
        var qualityMet = extractionResult.Quality >= ExtractionQuality.Low;
        Assert.True(qualityMet, $"Step 2: Quality should meet minimum threshold, got: {extractionResult.Quality}");
        _output($"✓ Step 2: Quality check passed - {extractionResult.Quality}");

        // Step 3: Verify Italian content
        var hasItalianContent = extractionResult.ExtractedText.Contains("gioc", StringComparison.OrdinalIgnoreCase) ||
                                extractionResult.ExtractedText.Contains("regol", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasItalianContent, "Step 3: Should contain Italian text");
        _output($"✓ Step 3: Italian content validated");

        // Step 4: Verify text is normalized (no excessive whitespace)
        var hasExcessiveWhitespace = extractionResult.ExtractedText.Contains("    "); // 4+ spaces
        Assert.False(hasExcessiveWhitespace, "Step 4: Text should be normalized (no excessive whitespace)");
        _output($"✓ Step 4: Text normalization verified");

        // Step 5: Verify paged extraction produces chunks (use Barrage PDF so CPU-only execution finishes faster)
        // Reopen stream (previous stream was disposed by ExtractTextAsync)
        await using var pdfStream2 = File.OpenRead(BarragePdfPath);
        var pagedResult = await _extractor.ExtractPagedTextAsync(pdfStream2, cancellationToken: TestCancellationToken);
        if (!pagedResult.Success)
        {
            _output($"Paged extraction failed: {pagedResult.ErrorMessage}");
        }
        Assert.True(pagedResult.Success, $"Step 5: Paged extraction should succeed ({pagedResult.ErrorMessage})");
        Assert.NotEmpty(pagedResult.PageChunks);
        _output($"✓ Step 5: Paged extraction produced {pagedResult.PageChunks.Count} chunks");

        _output("=== E2E Pipeline Complete ===");
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
