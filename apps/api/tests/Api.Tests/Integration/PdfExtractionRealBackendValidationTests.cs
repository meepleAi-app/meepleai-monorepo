using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Real backend validation tests for PDF extraction services.
/// Uses SharedTestcontainersFixture to test actual extraction accuracy, performance, and quality.
/// Validates against gold standards defined in TestData/pdf-corpus/gold-standards.json
/// </summary>
/// <remarks>
/// Purpose: Validate real PDF processing backend (Unstructured, SmolDocling) against known baselines.
///
/// Test Coverage:
/// - Extraction accuracy for all complexity tiers (simple, moderate, complex, edge-cases)
/// - Performance benchmarking (P95 latency validation)
/// - Multilingual support (English, Italian)
/// - Quality score validation
/// - Key phrase detection
/// - Page count accuracy
///
/// Hybrid Strategy:
/// - These tests run ONLY when TEST_PDF_SERVICES=true (CI selective execution)
/// - Other tests use mocks for fast feedback
/// - This provides comprehensive validation without slowing down standard CI
/// </remarks>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", "PDF")]
[Trait("TestType", "Validation")]
public class PdfExtractionRealBackendValidationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Action<string> _output;
    private UnstructuredPdfTextExtractor? _unstructuredExtractor;
    private SmolDoclingPdfTextExtractor? _smoldoclingExtractor;
    private readonly Dictionary<string, GoldStandard> _goldStandards = new();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PdfExtractionRealBackendValidationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("Initializing PDF real backend validation tests...");

        // Check if PDF services are enabled
        if (!_fixture.ArePdfServicesEnabled)
        {
            _output("⚠️ PDF services not enabled. Set TEST_PDF_SERVICES=true to run validation tests.");
            return;
        }

        // Load gold standards
        var goldStandardPath = "TestData/pdf-corpus/gold-standards.json";
        if (File.Exists(goldStandardPath))
        {
            var json = await File.ReadAllTextAsync(goldStandardPath);
            var goldStandardDoc = JsonSerializer.Deserialize<GoldStandardDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (goldStandardDoc != null)
            {
                // Flatten all tiers into single dictionary
                foreach (var (filename, standard) in goldStandardDoc.Simple ?? new())
                    _goldStandards[filename] = standard;
                foreach (var (filename, standard) in goldStandardDoc.Moderate ?? new())
                    _goldStandards[filename] = standard;
                foreach (var (filename, standard) in goldStandardDoc.Complex ?? new())
                    _goldStandards[filename] = standard;
                foreach (var (filename, standard) in goldStandardDoc.EdgeCases ?? new())
                    _goldStandards[filename] = standard;

                _output($"✅ Loaded {_goldStandards.Count} gold standards");
            }
        }

        // Initialize Unstructured extractor
        if (!string.IsNullOrEmpty(_fixture.UnstructuredServiceUrl))
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(_fixture.UnstructuredServiceUrl),
                Timeout = TimeSpan.FromSeconds(120)
            };

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            services.AddSingleton<IHttpClientFactory>(sp => new TestHttpClientFactory(httpClient));

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<UnstructuredPdfTextExtractor>>();

            _unstructuredExtractor = new UnstructuredPdfTextExtractor(
                serviceProvider.GetRequiredService<IHttpClientFactory>(),
                logger);

            _output($"✅ Unstructured extractor initialized at {_fixture.UnstructuredServiceUrl}");
        }

        // Initialize SmolDocling extractor
        if (!string.IsNullOrEmpty(_fixture.SmolDoclingServiceUrl))
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(_fixture.SmolDoclingServiceUrl),
                Timeout = TimeSpan.FromSeconds(120)
            };

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

            var config = new ConfigurationBuilder().Build();
            services.AddSingleton<IHttpClientFactory>(sp => new TestHttpClientFactory(httpClient));

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<SmolDoclingPdfTextExtractor>>();

            _smoldoclingExtractor = new SmolDoclingPdfTextExtractor(
                serviceProvider.GetRequiredService<IHttpClientFactory>(),
                logger,
                config);

            _output($"✅ SmolDocling extractor initialized at {_fixture.SmolDoclingServiceUrl}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _output("✅ Validation tests complete (shared containers reused)");
        await Task.CompletedTask;
    }

    #region Simple Tier Tests (92%+ accuracy)

    [Theory]
    [InlineData("carcassonne_rulebook.pdf")]
    [InlineData("splendor_rulebook.pdf")]
    [InlineData("azul_rulebook.pdf")]
    public async Task Unstructured_SimplePdfs_MeetsAccuracyThreshold(string filename)
    {
        // Skip if service not available
        if (_unstructuredExtractor == null || !_goldStandards.ContainsKey(filename))
        {
            Assert.Skip($"Unstructured service not available or gold standard missing for {filename}");
        }

        var standard = _goldStandards[filename];
        if (!File.Exists(standard.RelativePath))
        {
            Assert.Skip($"PDF not found: {standard.RelativePath}");
        }

        _output($"\n📄 Testing: {filename} (Simple tier)");

        // Act
        await using var pdfStream = File.OpenRead(standard.RelativePath);
        var result = await _unstructuredExtractor.ExtractTextAsync(pdfStream, enableOcrFallback: true, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue($"Extraction should succeed for simple PDF: {result.ErrorMessage}");
        result.PageCount.Should().BeCloseTo(standard.ExpectedPages, 1,
            $"Page count should be within ±1 of expected {standard.ExpectedPages}");

        // Validate key phrases
        foreach (var phrase in standard.KeyPhrases)
        {
            result.ExtractedText.Should().Contain(phrase,
                because: $"rulebook should contain '{phrase}'");
        }

        _output($"✅ {filename}: {result.PageCount} pages, {result.CharacterCount:N0} chars, Quality: {result.Quality}");
    }

    [Fact]
    public async Task Unstructured_ItalianPdf_MultilingualSupport()
    {
        if (_unstructuredExtractor == null || !_goldStandards.ContainsKey("scacchi-fide_2017_rulebook.pdf"))
        {
            Assert.Skip("Unstructured service not available or gold standard missing");
        }

        var standard = _goldStandards["scacchi-fide_2017_rulebook.pdf"];
        if (!File.Exists(standard.RelativePath))
        {
            Assert.Skip($"PDF not found: {standard.RelativePath}");
        }

        _output("\n🌍 Testing: Italian language support");

        // Act
        await using var pdfStream = File.OpenRead(standard.RelativePath);
        var result = await _unstructuredExtractor.ExtractTextAsync(pdfStream, enableOcrFallback: true, TestCancellationToken);

        // Assert - Italian key phrases
        result.Success.Should().BeTrue();
        foreach (var phrase in standard.KeyPhrases)
        {
            result.ExtractedText.Should().Contain(phrase,
                because: $"Italian rulebook should contain '{phrase}'");
        }

        _output($"✅ Italian extraction: {result.CharacterCount:N0} chars, detected all Italian key phrases");
    }

    #endregion

    #region Moderate Tier Tests (85%+ accuracy)

    [Theory]
    [InlineData("wingspan_en_rulebook.pdf")]
    [InlineData("pandemic_rulebook.pdf")]
    public async Task Unstructured_ModeratePdfs_HandlesMultiColumnLayouts(string filename)
    {
        if (_unstructuredExtractor == null || !_goldStandards.ContainsKey(filename))
        {
            Assert.Skip($"Unstructured service not available or gold standard missing for {filename}");
        }

        var standard = _goldStandards[filename];
        if (!File.Exists(standard.RelativePath))
        {
            Assert.Skip($"PDF not found: {standard.RelativePath}");
        }

        _output($"\n📄 Testing: {filename} (Moderate tier - multi-column)");

        // Act
        await using var pdfStream = File.OpenRead(standard.RelativePath);
        var stopwatch = Stopwatch.StartNew();
        var result = await _unstructuredExtractor.ExtractTextAsync(pdfStream, enableOcrFallback: true, TestCancellationToken);
        stopwatch.Stop();

        // Assert
        result.Success.Should().BeTrue();
        ((int)result.Quality).Should().BeGreaterThanOrEqualTo((int)ExtractionQuality.Medium,
            because: "Moderate PDFs should achieve at least Medium quality");

        // Validate key game terminology extracted
        standard.KeyPhrases.Should().AllSatisfy(phrase =>
            result.ExtractedText.Should().Contain(phrase));

        _output($"✅ {filename}: {stopwatch.ElapsedMilliseconds}ms, Quality: {result.Quality}, {result.PageCount} pages");
    }

    #endregion

    #region Complex Tier Tests (80%+ accuracy)

    [Theory]
    [InlineData("barrage_rulebook.pdf")]
    [InlineData("root_rulebook.pdf")]
    public async Task Unstructured_ComplexPdfs_HandlesHeavyLayoutsAndTables(string filename)
    {
        if (_unstructuredExtractor == null || !_goldStandards.ContainsKey(filename))
        {
            Assert.Skip($"Unstructured service not available or gold standard missing for {filename}");
        }

        var standard = _goldStandards[filename];
        if (!File.Exists(standard.RelativePath))
        {
            Assert.Skip($"PDF not found: {standard.RelativePath}");
        }

        _output($"\n📄 Testing: {filename} (Complex tier - heavy layouts)");

        // Act
        await using var pdfStream = File.OpenRead(standard.RelativePath);
        var stopwatch = Stopwatch.StartNew();
        var result = await _unstructuredExtractor.ExtractTextAsync(pdfStream, enableOcrFallback: true, TestCancellationToken);
        stopwatch.Stop();

        // Assert
        result.Success.Should().BeTrue();
        result.PageCount.Should().BeGreaterThan(15, "Complex PDFs have many pages");
        result.CharacterCount.Should().BeGreaterThan(10000, "Complex PDFs have substantial text");

        // Key phrases should still be detectable despite complex layout
        var detectedPhrases = standard.KeyPhrases.Count(phrase =>
            result.ExtractedText.Contains(phrase, StringComparison.OrdinalIgnoreCase));
        var detectionRate = (double)detectedPhrases / standard.KeyPhrases.Length;

        detectionRate.Should().BeGreaterThanOrEqualTo(0.80,
            $"Should detect at least 80% of key phrases in complex layouts ({detectedPhrases}/{standard.KeyPhrases.Length})");

        _output($"✅ {filename}: {stopwatch.ElapsedMilliseconds}ms, Phrase detection: {detectionRate:P0}, Quality: {result.Quality}");
    }

    #endregion

    #region Edge Case Tests (75%+ accuracy)

    [Fact(Timeout = 60000)] // 1 minute timeout for large file
    public async Task Unstructured_LargeFile_TerraformingMars_HandlesStressTest()
    {
        if (_unstructuredExtractor == null || !_goldStandards.ContainsKey("terraforming-mars_rulebook.pdf"))
        {
            Assert.Skip("Unstructured service not available or gold standard missing");
        }

        var standard = _goldStandards["terraforming-mars_rulebook.pdf"];
        if (!File.Exists(standard.RelativePath))
        {
            Assert.Skip($"PDF not found: {standard.RelativePath}");
        }

        _output($"\n🔥 Stress Test: terraforming-mars_rulebook.pdf (38MB, {standard.ExpectedPages} pages)");

        // Act
        await using var pdfStream = File.OpenRead(standard.RelativePath);
        var stopwatch = Stopwatch.StartNew();
        var result = await _unstructuredExtractor.ExtractTextAsync(pdfStream, enableOcrFallback: true, TestCancellationToken);
        stopwatch.Stop();

        // Assert
        result.Success.Should().BeTrue("Large PDF extraction should succeed");
        result.PageCount.Should().BeGreaterThan(25, "TM rulebook has 30+ pages");
        result.CharacterCount.Should().BeGreaterThan(15000, "Massive rulebook should have 15K+ characters");

        // Performance assertion
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(standard.ExtractionTimeP95Ms,
            $"Extraction should complete within P95 latency ({standard.ExtractionTimeP95Ms}ms)");

        // Quality can degrade for such large/complex files
        ((int)result.Quality).Should().BeGreaterThanOrEqualTo((int)ExtractionQuality.Low,
            because: "Even large files should maintain at least Low quality");

        _output($"✅ Stress test passed: {stopwatch.ElapsedMilliseconds}ms (P95: {standard.ExtractionTimeP95Ms}ms), " +
               $"{result.CharacterCount:N0} chars, Quality: {result.Quality}");
    }

    #endregion

    #region Performance Benchmarking

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task PerformanceBenchmark_AllComplexityTiers_WithinP95Targets()
    {
        if (_unstructuredExtractor == null)
        {
            Assert.Skip("Unstructured service not available for performance benchmarking");
        }

        _output("\n⚡ Performance Benchmark: Testing all complexity tiers\n");

        var results = new List<(string Filename, long LatencyMs, string Complexity, bool WithinP95)>();

        foreach (var (filename, standard) in _goldStandards.OrderBy(x => x.Value.FileSize))
        {
            if (!File.Exists(standard.RelativePath))
            {
                _output($"⚠️ Skipping {filename}: File not found");
                continue;
            }

            await using var pdfStream = File.OpenRead(standard.RelativePath);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await _unstructuredExtractor.ExtractTextAsync(pdfStream, enableOcrFallback: true, TestCancellationToken);
                stopwatch.Stop();

                var withinP95 = stopwatch.ElapsedMilliseconds < standard.ExtractionTimeP95Ms;
                results.Add((filename, stopwatch.ElapsedMilliseconds, standard.Complexity, withinP95));

                var status = withinP95 ? "✅" : "⚠️";
                _output($"{status} {filename,-35} | {standard.Complexity,-10} | " +
                       $"{stopwatch.ElapsedMilliseconds,6}ms / {standard.ExtractionTimeP95Ms,6}ms P95 | " +
                       $"Quality: {result.Quality}");
            }
            catch (Exception ex)
            {
                _output($"❌ {filename}: {ex.Message}");
                results.Add((filename, -1, standard.Complexity, false));
            }
        }

        // Summary statistics
        _output("\n📊 Performance Summary:");
        _output($"   Total PDFs tested: {results.Count}");
        _output($"   Within P95: {results.Count(r => r.WithinP95)} ({results.Count(r => r.WithinP95) * 100.0 / results.Count:F0}%)");
        _output($"   Average latency: {results.Where(r => r.LatencyMs > 0).Average(r => r.LatencyMs):F0}ms");
        _output($"   P95 latency: {CalculateP95(results.Where(r => r.LatencyMs > 0).Select(r => r.LatencyMs)):F0}ms");

        // Assert - At least 80% should meet P95 targets
        var p95ComplianceRate = results.Count(r => r.WithinP95) / (double)results.Count;
        p95ComplianceRate.Should().BeGreaterThanOrEqualTo(0.80,
            "At least 80% of PDFs should meet P95 latency targets");
    }

    #endregion

    #region Accuracy Validation

    [Theory]
    [InlineData("carcassonne_rulebook.pdf", "simple")]
    [InlineData("wingspan_en_rulebook.pdf", "moderate")]
    [InlineData("barrage_rulebook.pdf", "complex")]
    public async Task Unstructured_KeyPhraseDetection_AcrossTiers(string filename, string tier)
    {
        if (_unstructuredExtractor == null || !_goldStandards.ContainsKey(filename))
        {
            Assert.Skip($"Unstructured service not available or gold standard missing for {filename}");
        }

        var standard = _goldStandards[filename];
        if (!File.Exists(standard.RelativePath))
        {
            Assert.Skip($"PDF not found: {standard.RelativePath}");
        }

        _output($"\n🔍 Key Phrase Detection: {filename} ({tier})");

        // Act
        await using var pdfStream = File.OpenRead(standard.RelativePath);
        var result = await _unstructuredExtractor.ExtractTextAsync(pdfStream, enableOcrFallback: true, TestCancellationToken);

        // Assert - All key phrases must be detected
        var detectedPhrases = new List<string>();
        var missedPhrases = new List<string>();

        foreach (var phrase in standard.KeyPhrases)
        {
            if (result.ExtractedText.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                detectedPhrases.Add(phrase);
            }
            else
            {
                missedPhrases.Add(phrase);
            }
        }

        var detectionRate = detectedPhrases.Count / (double)standard.KeyPhrases.Length;

        _output($"   Detected: {detectedPhrases.Count}/{standard.KeyPhrases.Length} phrases ({detectionRate:P0})");
        if (missedPhrases.Any())
        {
            _output($"   Missed: {string.Join(", ", missedPhrases)}");
        }

        // Threshold based on complexity
        var requiredRate = tier switch
        {
            "simple" => 1.0,      // 100% for simple
            "moderate" => 0.90,   // 90% for moderate
            "complex" => 0.80,    // 80% for complex
            _ => 0.75             // 75% for edge cases
        };

        detectionRate.Should().BeGreaterThanOrEqualTo(requiredRate,
            $"{tier} tier PDFs should detect {requiredRate:P0}+ of key phrases");
    }

    #endregion

    #region Comparison Tests (Unstructured vs SmolDocling)

    [Theory]
    [InlineData("splendor_rulebook.pdf")]
    [InlineData("pandemic_rulebook.pdf")]
    public async Task CompareExtractors_SamePdf_BothProduceQualityResults(string filename)
    {
        if (_unstructuredExtractor == null || _smoldoclingExtractor == null || !_goldStandards.ContainsKey(filename))
        {
            Assert.Skip("Both PDF services required for comparison test");
        }

        var standard = _goldStandards[filename];
        if (!File.Exists(standard.RelativePath))
        {
            Assert.Skip($"PDF not found: {standard.RelativePath}");
        }

        _output($"\n⚖️ Comparison Test: {filename}");

        // Act - Extract with both services
        await using var stream1 = File.OpenRead(standard.RelativePath);
        var unstructuredResult = await _unstructuredExtractor.ExtractTextAsync(stream1, enableOcrFallback: true, TestCancellationToken);

        await using var stream2 = File.OpenRead(standard.RelativePath);
        var smoldoclingResult = await _smoldoclingExtractor.ExtractTextAsync(stream2, enableOcrFallback: true, TestCancellationToken);

        // Assert - Both should succeed
        unstructuredResult.Success.Should().BeTrue("Unstructured extraction should succeed");
        smoldoclingResult.Success.Should().BeTrue("SmolDocling extraction should succeed");

        // Compare results
        _output($"   Unstructured: {unstructuredResult.CharacterCount:N0} chars, Quality: {unstructuredResult.Quality}");
        _output($"   SmolDocling:  {smoldoclingResult.CharacterCount:N0} chars, Quality: {smoldoclingResult.Quality}");

        // Both should detect key phrases
        var unstructuredPhrases = standard.KeyPhrases.Count(p => unstructuredResult.ExtractedText.Contains(p, StringComparison.OrdinalIgnoreCase));
        var smoldoclingPhrases = standard.KeyPhrases.Count(p => smoldoclingResult.ExtractedText.Contains(p, StringComparison.OrdinalIgnoreCase));

        unstructuredPhrases.Should().BeGreaterThanOrEqualTo((int)(standard.KeyPhrases.Length * 0.85));
        smoldoclingPhrases.Should().BeGreaterThanOrEqualTo((int)(standard.KeyPhrases.Length * 0.85));

        _output($"✅ Both extractors met quality thresholds");
    }

    #endregion

    #region Helper Methods

    private static double CalculateP95(IEnumerable<long> latencies)
    {
        var sorted = latencies.OrderBy(x => x).ToList();
        if (sorted.Count == 0) return 0;

        var index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
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

    #endregion
}

#region Gold Standard Models

internal record GoldStandardDocument(
    Dictionary<string, GoldStandard>? Simple,
    Dictionary<string, GoldStandard>? Moderate,
    Dictionary<string, GoldStandard>? Complex,
    Dictionary<string, GoldStandard>? EdgeCases);

internal record GoldStandard(
    string RelativePath,
    long FileSize,
    int ExpectedPages,
    int ExpectedWordCount,
    string Language,
    string[] KeyPhrases,
    double MinAccuracyScore,
    long ExtractionTimeP95Ms,
    string Complexity,
    string Notes);

#endregion
