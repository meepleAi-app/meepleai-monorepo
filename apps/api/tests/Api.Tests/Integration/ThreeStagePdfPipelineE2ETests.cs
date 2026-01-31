using System.Diagnostics;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration;

/// <summary>
/// End-to-end integration tests for 3-stage PDF extraction pipeline
/// Tests orchestrator with real Docker services (Unstructured + SmolDocling) + local Docnet
/// </summary>
/// <remarks>
/// Issue #950: BGAI-011 - Comprehensive E2E testing of PDF pipeline
/// Architecture: EnhancedPdfProcessingOrchestrator → 3 stages with quality-based fallback
/// Hybrid approach: Tests 1-5 use fake extractors (fast), Test 6 uses Testcontainers (real performance)
/// </remarks>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.E2E)]
public class ThreeStagePdfPipelineE2ETests : IAsyncLifetime
{
    private readonly Action<string> _output;
    private IContainer? _unstructuredContainer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EnhancedPdfProcessingOrchestrator> _logger;
    private readonly IOptions<PdfProcessingOptions> _options;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test PDF paths (relative to bin/Debug/net9.0/)
    // NOTE: Temporarily disabled until Docker images are built in CI
    private const string BarragePdfPath = "../../../../data/barrage_rulebook.pdf";
    private const int RealServiceTargetP95LatencyMs = 35_000; // CPU-only Testcontainers need more headroom than 5s

    public ThreeStagePdfPipelineE2ETests()
    {
        _output = Console.WriteLine;
        _configuration = new ConfigurationBuilder().Build();
        _logger = Mock.Of<ILogger<EnhancedPdfProcessingOrchestrator>>();
        _options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52428800,
            UseTempFileForLargePdfs = true
        });
    }

    public async ValueTask InitializeAsync()
    {
        _output("E2E test infrastructure initializing...");
        // Testcontainers will be started only for Test 6 (performance test)
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _output("Cleaning up E2E test infrastructure...");

        if (_unstructuredContainer != null)
        {
            await _unstructuredContainer.StopAsync(TestCancellationToken);
            await _unstructuredContainer.DisposeAsync();
        }



        _output("Cleanup complete");
    }
    [Fact]
    public async Task HappyPath_Stage1HighQuality_ReturnsStage1Result()
    {
        // Arrange - Stage 1 succeeds with high quality (0.85 ≥ 0.80)
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, name: "Unstructured");
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, name: "SmolDocling");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, name: "Docnet");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(stage1, stage2, stage3, _logger, _configuration, _options);

        await using var pdfStream = CreateDummyPdfStream();

        _output("Test 1: Happy path - Stage 1 high quality");

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success, "Happy path should succeed");
        Assert.Equal(1, result.StageUsed);
        Assert.Equal("Unstructured", result.StageName);
        Assert.Equal(ExtractionQuality.High, result.Quality);
        Assert.True(result.TotalDurationMs >= 0);

        // Only Stage 1 should be called
        Assert.Equal(1, stage1.CallCount);
        Assert.Equal(0, stage2.CallCount);
        Assert.Equal(0, stage3.CallCount);

        _output($"✓ Test 1 passed: Stage {result.StageUsed} ({result.StageName}), Quality: {result.Quality}, Duration: {result.TotalDurationMs}ms");
    }
    [Fact]
    public async Task Fallback_Stage1LowQuality_FallsBackToStage2()
    {
        // Arrange - Stage 1 low quality (0.50 < 0.80), Stage 2 high quality (0.85 ≥ 0.70)
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, name: "Unstructured");
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.High, name: "SmolDocling");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, name: "Docnet");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(stage1, stage2, stage3, _logger, _configuration, _options);

        await using var pdfStream = CreateDummyPdfStream();

        _output("Test 2: Fallback - Stage 1 low quality → Stage 2");

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.StageUsed);
        Assert.Equal("SmolDocling", result.StageName);
        Assert.Equal(ExtractionQuality.High, result.Quality);

        // Stage 1 and 2 called, Stage 3 NOT called
        Assert.Equal(1, stage1.CallCount);
        Assert.Equal(1, stage2.CallCount);
        Assert.Equal(0, stage3.CallCount);

        _output($"✓ Test 2 passed: Fallback to Stage {result.StageUsed}, Quality improved from Low to {result.Quality}");
    }
    [Fact]
    public async Task Fallback_Stage1And2Fail_FallsBackToStage3()
    {
        // Arrange - Stages 1-2 fail, Stage 3 succeeds
        var stage1 = new FakeExtractor(success: false, name: "Unstructured", errorMsg: "Service timeout");
        var stage2 = new FakeExtractor(success: false, name: "SmolDocling", errorMsg: "Circuit breaker open");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, name: "Docnet");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(stage1, stage2, stage3, _logger, _configuration, _options);

        await using var pdfStream = CreateDummyPdfStream();

        _output("Test 3: Fallback - Stage 1&2 fail → Stage 3");

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.StageUsed);
        Assert.Equal("Docnet", result.StageName);

        // All 3 stages attempted
        Assert.Equal(1, stage1.CallCount);
        Assert.Equal(1, stage2.CallCount);
        Assert.Equal(1, stage3.CallCount);

        _output($"✓ Test 3 passed: All stages attempted, Stage 3 succeeded as fallback");
    }
    [Fact]
    public async Task QualityGate_Stage1LowToStage2High_EnforcesThreshold()
    {
        // Arrange - Test quality gate logic explicitly
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, name: "Unstructured"); // 0.50 < 0.80
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.High, name: "SmolDocling"); // 0.85 ≥ 0.70
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, name: "Docnet");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(stage1, stage2, stage3, _logger, _configuration, _options);

        await using var pdfStream = CreateDummyPdfStream();

        _output("Test 4: Quality gate - Stage 1 (0.50) rejected, Stage 2 (0.85) accepted");

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert - Quality gate should trigger fallback
        Assert.True(result.Success);
        Assert.Equal(2, result.StageUsed); // Stage 2 accepted (0.85 ≥ 0.70)
        Assert.Equal(ExtractionQuality.High, result.Quality);

        // Verify quality gate triggered fallback
        Assert.Equal(1, stage1.CallCount); // Stage 1 tried but rejected
        Assert.Equal(1, stage2.CallCount); // Stage 2 accepted
        Assert.Equal(0, stage3.CallCount); // Stage 3 not needed

        _output($"✓ Test 4 passed: Quality gate enforced, Stage 2 quality ({result.Quality}) meets threshold");
    }
    [Fact]
    public async Task AllStagesFail_ReturnsStage3ErrorResult()
    {
        // Arrange - All stages fail
        var stage1 = new FakeExtractor(success: false, name: "Unstructured", errorMsg: "Connection refused");
        var stage2 = new FakeExtractor(success: false, name: "SmolDocling", errorMsg: "GPU unavailable");
        var stage3 = new FakeExtractor(success: false, name: "Docnet", errorMsg: "Corrupted PDF");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(stage1, stage2, stage3, _logger, _configuration, _options);

        await using var pdfStream = CreateDummyPdfStream();

        _output("Test 5: All stages fail - error handling");

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert - Returns Stage 3 result (even if failed)
        Assert.False(result.Success);
        Assert.Equal(3, result.StageUsed);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Corrupted PDF", result.ErrorMessage);

        // All 3 stages attempted
        Assert.Equal(1, stage1.CallCount);
        Assert.Equal(1, stage2.CallCount);
        Assert.Equal(1, stage3.CallCount);

        _output($"✓ Test 5 passed: All stages failed gracefully, error: {result.ErrorMessage}");
    }
    [Fact]
    public async Task Performance_P95Latency_WithRealServices()
    {
        // This test uses real Testcontainers for performance measurement
        if (!File.Exists(BarragePdfPath))
        {
            Assert.Skip($"Test PDF not found: {BarragePdfPath}");
        }

        _output("Test 6: Performance P95 latency with real Docker services");
        _output("Starting Testcontainers (this may take 2-3 minutes)...");

        // Start Unstructured service
        _unstructuredContainer = new ContainerBuilder()
            .WithImage("infra-unstructured-service:latest")
            .WithPortBinding(8001, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/health")
                    .ForPort(8001)
                    .ForStatusCode(System.Net.HttpStatusCode.OK)))
            .Build();

        await _unstructuredContainer.StartAsync(TestCancellationToken);
        var unstructuredPort = _unstructuredContainer.GetMappedPublicPort(8001);
        _output($"✓ Unstructured service started on port {unstructuredPort}");

        // Create real extractors
        var unstructuredClient = new HttpClient { BaseAddress = new Uri($"http://localhost:{unstructuredPort}") };
        var unstructuredExtractor = new UnstructuredPdfTextExtractor(
            new TestHttpClientFactory(unstructuredClient),
            Mock.Of<ILogger<UnstructuredPdfTextExtractor>>());

        var smoldoclingExtractor = new FakeExtractor(success: false, name: "SmolDocling"); // Skip Stage 2 for speed
        var docnetExtractor = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, name: "Docnet");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            unstructuredExtractor,
            smoldoclingExtractor,
            docnetExtractor,
            _logger,
            _configuration,
            _options);

        // Measure P95 latency over 10 iterations (reduced from 20 for test speed)
        const int iterations = 10;
        var latencies = new List<long>();

        _output($"Running {iterations} iterations for P95 measurement...");

        for (int i = 0; i < iterations; i++)
        {
            await using var pdfStream = File.OpenRead(BarragePdfPath);
            var sw = Stopwatch.StartNew();

            var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, cancellationToken: TestCancellationToken);

            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);

            Assert.True(result.Success, $"Iteration {i + 1} should succeed");
            _output($"  Iteration {i + 1}: {sw.ElapsedMilliseconds}ms (Stage {result.StageUsed})");
        }

        // Calculate P95
        latencies.Sort();
        var p95Index = (int)Math.Ceiling(iterations * 0.95) - 1;
        var p95Latency = latencies[p95Index];
        var avgLatency = latencies.Average();

        _output($"Performance Results: Avg={avgLatency:F0}ms, P95={p95Latency}ms (target {RealServiceTargetP95LatencyMs}ms for CPU-only Docker stack)");

        // Assert - Allow higher P95 on CPU-bound test nodes; GPU-backed CI can tighten this later
        Assert.True(
            p95Latency < RealServiceTargetP95LatencyMs,
            $"P95 latency ({p95Latency}ms) should be < {RealServiceTargetP95LatencyMs}ms (CPU-only Testcontainers)");

        _output($"✓ Test 6 passed: P95={p95Latency}ms < {RealServiceTargetP95LatencyMs}ms target");

        // Cleanup
        unstructuredClient.Dispose();
    }
    /// <summary>
    /// Fake PDF extractor for controlled testing without real services
    /// </summary>
    private class FakeExtractor : IPdfTextExtractor
    {
        private readonly bool _success;
        private readonly ExtractionQuality _quality;
        private readonly string _name;
        private readonly string? _errorMsg;

        public int CallCount { get; private set; }
        public int PagedCallCount { get; private set; }

        public FakeExtractor(
            bool success = true,
            ExtractionQuality quality = ExtractionQuality.High,
            string name = "FakeExtractor",
            string? errorMsg = null)
        {
            _success = success;
            _quality = quality;
            _name = name;
            _errorMsg = errorMsg;
        }

        public Task<TextExtractionResult> ExtractTextAsync(
            Stream pdfStream,
            bool enableOcrFallback = true,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (_success)
            {
                return Task.FromResult(TextExtractionResult.CreateSuccess(
                    extractedText: $"Sample text from {_name}",
                    pageCount: 10,
                    characterCount: 5000,
                    ocrTriggered: false,
                    quality: _quality));
            }

            return Task.FromResult(TextExtractionResult.CreateFailure(_errorMsg ?? $"{_name} failed"));
        }

        public Task<PagedTextExtractionResult> ExtractPagedTextAsync(
            Stream pdfStream,
            bool enableOcrFallback = true,
            CancellationToken cancellationToken = default)
        {
            PagedCallCount++;

            if (_success)
            {
                var chunks = Enumerable.Range(1, 10)
                    .Select(i => new PageTextChunk(
                        PageNumber: i,
                        Text: $"Page {i} from {_name}",
                        CharStartIndex: (i - 1) * 100,
                        CharEndIndex: i * 100))
                    .ToList();

                return Task.FromResult(PagedTextExtractionResult.CreateSuccess(
                    pageChunks: chunks,
                    totalPages: 10,
                    totalCharacters: 1000,
                    ocrTriggered: false));
            }

            return Task.FromResult(PagedTextExtractionResult.CreateFailure(_errorMsg ?? $"{_name} paged failed"));
        }
    }
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
    private static MemoryStream CreateDummyPdfStream()
    {
        // Minimal PDF header
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
        return new MemoryStream(pdfBytes);
    }
}
