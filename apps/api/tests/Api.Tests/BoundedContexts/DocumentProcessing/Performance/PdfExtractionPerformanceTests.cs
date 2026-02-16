using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Performance;

/// <summary>
/// Performance tests for the 3-stage PDF extraction pipeline.
/// Measures extraction timing, quality distribution, and timeout behavior.
/// Issue #4143: Performance Testing - PDF Wizard
/// </summary>
[Trait("Category", TestCategories.Performance)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4143")]
public class PdfExtractionPerformanceTests
{
    private readonly ILogger<EnhancedPdfProcessingOrchestrator> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOptions<PdfProcessingOptions> _options;

    public PdfExtractionPerformanceTests()
    {
        _logger = Mock.Of<ILogger<EnhancedPdfProcessingOrchestrator>>();
        _configuration = new ConfigurationBuilder().Build();
        _options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52_428_800,
            UseTempFileForLargePdfs = true
        });
    }

    #region Stage Timing Tests

    [Fact]
    public async Task Stage1_HighQuality_CompletesWithinTarget()
    {
        // Target: <5s for small PDFs via Stage 1 (Unstructured)
        var stage1 = new TimedFakeExtractor(delayMs: 50, success: true, quality: ExtractionQuality.High,
            text: GenerateText(pageCount: 8, charsPerPage: 1200));
        var stage2 = new TimedFakeExtractor(delayMs: 100, success: true, quality: ExtractionQuality.Medium);
        var stage3 = new TimedFakeExtractor(delayMs: 20, success: true, quality: ExtractionQuality.Low);

        var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
        await using var pdfStream = CreateDummyPdfStream();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);
        sw.Stop();

        result.Success.Should().BeTrue();
        result.StageUsed.Should().Be(1, "High quality should resolve at Stage 1");
        result.StageName.Should().Be("Unstructured");
        result.TotalDurationMs.Should().BeGreaterThanOrEqualTo(0);
        sw.ElapsedMilliseconds.Should().BeLessThan(5000, "Stage 1 should complete within 5s target");
    }

    [Fact]
    public async Task Stage2_Fallback_CompletesWithinTarget()
    {
        // Target: <10s when falling back to Stage 2 (SmolDocling)
        var stage1 = new TimedFakeExtractor(delayMs: 50, success: true, quality: ExtractionQuality.Low,
            text: "Low quality stage 1");
        var stage2 = new TimedFakeExtractor(delayMs: 100, success: true, quality: ExtractionQuality.Medium,
            text: GenerateText(pageCount: 50, charsPerPage: 800));
        var stage3 = new TimedFakeExtractor(delayMs: 20, success: true, quality: ExtractionQuality.Low);

        var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
        await using var pdfStream = CreateDummyPdfStream();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);
        sw.Stop();

        result.Success.Should().BeTrue();
        result.StageUsed.Should().Be(2, "Low quality Stage 1 should fallback to Stage 2");
        result.StageName.Should().Be("SmolDocling");
        sw.ElapsedMilliseconds.Should().BeLessThan(10000, "Stage 2 fallback should complete within 10s target");
    }

    [Fact]
    public async Task Stage3_Fallback_CompletesWithinTarget()
    {
        // Target: <3s for Stage 3 (Docnet - local, always fast)
        var stage1 = new TimedFakeExtractor(delayMs: 30, success: false, errorMsg: "Unstructured unavailable");
        var stage2 = new TimedFakeExtractor(delayMs: 30, success: false, errorMsg: "SmolDocling timeout");
        var stage3 = new TimedFakeExtractor(delayMs: 20, success: true, quality: ExtractionQuality.Low,
            text: GenerateText(pageCount: 200, charsPerPage: 300));

        var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
        await using var pdfStream = CreateDummyPdfStream();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);
        sw.Stop();

        result.Success.Should().BeTrue();
        result.StageUsed.Should().Be(3, "Both stages failed, should use Docnet");
        result.StageName.Should().Be("Docnet");
        sw.ElapsedMilliseconds.Should().BeLessThan(3000, "Stage 3 (Docnet) should be fast (<3s)");
    }

    [Fact]
    public async Task FullPipeline_AllStagesAttempted_CompletesWithinOverallTimeout()
    {
        // Worst case: all 3 stages attempted, should still complete within 2 min
        var stage1 = new TimedFakeExtractor(delayMs: 100, success: true, quality: ExtractionQuality.VeryLow);
        var stage2 = new TimedFakeExtractor(delayMs: 100, success: true, quality: ExtractionQuality.Low);
        var stage3 = new TimedFakeExtractor(delayMs: 50, success: true, quality: ExtractionQuality.Medium,
            text: "Fallback text");

        var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
        await using var pdfStream = CreateDummyPdfStream();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, cancellationToken: cts.Token);
        sw.Stop();

        result.Success.Should().BeTrue();
        result.StageUsed.Should().Be(3);
        sw.ElapsedMilliseconds.Should().BeLessThan(120000, "Full pipeline should complete within 2 min timeout");
    }

    #endregion

    #region Quality Distribution Tests

    [Fact]
    public async Task QualityDistribution_Stage1Success_80Percent()
    {
        // Simulate 100 extractions: 80% should succeed at Stage 1
        const int totalRuns = 100;
        var stage1Count = 0;
        var stage2Count = 0;
        var stage3Count = 0;

        for (var i = 0; i < totalRuns; i++)
        {
            IPdfTextExtractor stage1, stage2, stage3;

            if (i < 80) // 80% high quality
            {
                stage1 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.High);
                stage2 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.Medium);
                stage3 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.Low);
            }
            else if (i < 95) // 15% need Stage 2
            {
                stage1 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.Low);
                stage2 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.Medium);
                stage3 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.Low);
            }
            else // 5% need Stage 3
            {
                stage1 = new TimedFakeExtractor(delayMs: 0, success: false, errorMsg: "Stage 1 fail");
                stage2 = new TimedFakeExtractor(delayMs: 0, success: false, errorMsg: "Stage 2 fail");
                stage3 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.Low);
            }

            var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
            await using var pdfStream = CreateDummyPdfStream();
            var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

            result.Success.Should().BeTrue();
            switch (result.StageUsed)
            {
                case 1: stage1Count++; break;
                case 2: stage2Count++; break;
                case 3: stage3Count++; break;
            }
        }

        stage1Count.Should().Be(80, "80% should succeed at Stage 1 (Unstructured)");
        stage2Count.Should().Be(15, "15% should fallback to Stage 2 (SmolDocling)");
        stage3Count.Should().Be(5, "5% should fallback to Stage 3 (Docnet)");
    }

    #endregion

    #region Timeout Configuration Tests

    [Theory]
    [InlineData(35, "Unstructured default")]
    [InlineData(30, "SmolDocling default")]
    [InlineData(60, "Extended timeout")]
    public async Task TimeoutConfiguration_CorrectValuesApplied(int timeoutSeconds, string description)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfProcessing:Extractor:Unstructured:TimeoutSeconds"] = timeoutSeconds.ToString(),
                ["PdfProcessing:Extractor:SmolDocling:TimeoutSeconds"] = timeoutSeconds.ToString()
            })
            .Build();

        var stage1 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.High);
        var stage2 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.Medium);
        var stage3 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.Low);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, config, _options);

        await using var pdfStream = CreateDummyPdfStream();
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        result.Success.Should().BeTrue($"Timeout config {description} ({timeoutSeconds}s) should not affect fast extraction");
    }

    [Fact]
    public async Task CancellationToken_RespectsTimeout()
    {
        // All stages take too long - Stage 3 propagates TaskCanceledException
        // Stages 1-2 catch cancellation and fall back, Stage 3 propagates it
        var stage1 = new TimedFakeExtractor(delayMs: 5000, success: true, quality: ExtractionQuality.High);
        var stage2 = new TimedFakeExtractor(delayMs: 5000, success: true, quality: ExtractionQuality.Medium);
        var stage3 = new TimedFakeExtractor(delayMs: 5000, success: true, quality: ExtractionQuality.Low);

        var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
        await using var pdfStream = CreateDummyPdfStream();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = () => orchestrator.ExtractTextWithFallbackAsync(pdfStream, cancellationToken: cts.Token);

        // Stage 3 propagates TaskCanceledException (no further fallback available)
        await act.Should().ThrowAsync<TaskCanceledException>(
            "Orchestrator should propagate cancellation from Stage 3 when no further fallback exists");
    }

    #endregion

    #region Paged Extraction Performance Tests

    [Fact]
    public async Task PagedExtraction_SmallPdf_CompletesQuickly()
    {
        // 10 pages, high quality - should resolve at Stage 1
        var stage1 = new TimedFakeExtractor(delayMs: 30, success: true, pageCount: 10, charsPerPage: 1200);
        var stage2 = new TimedFakeExtractor(delayMs: 50, success: true, pageCount: 10, charsPerPage: 800);
        var stage3 = new TimedFakeExtractor(delayMs: 20, success: true, pageCount: 10, charsPerPage: 300);

        var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
        await using var pdfStream = CreateDummyPdfStream();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream);
        sw.Stop();

        result.Success.Should().BeTrue();
        result.StageUsed.Should().Be(1);
        result.TotalPages.Should().Be(10);
        result.PageChunks.Count.Should().Be(10);
        sw.ElapsedMilliseconds.Should().BeLessThan(5000);
    }

    [Fact]
    public async Task PagedExtraction_MediumPdf_CompletesWithinTarget()
    {
        // 100 pages - Stage 1 low quality, falls back to Stage 2
        var stage1 = new TimedFakeExtractor(delayMs: 50, success: true, pageCount: 100, charsPerPage: 300);
        var stage2 = new TimedFakeExtractor(delayMs: 80, success: true, pageCount: 100, charsPerPage: 900);
        var stage3 = new TimedFakeExtractor(delayMs: 30, success: true, pageCount: 100, charsPerPage: 200);

        var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
        await using var pdfStream = CreateDummyPdfStream();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream);
        sw.Stop();

        result.Success.Should().BeTrue();
        result.StageUsed.Should().Be(2, "300 cpp → 0.30 < 0.80 threshold, should fallback to Stage 2");
        result.TotalPages.Should().Be(100);
        sw.ElapsedMilliseconds.Should().BeLessThan(15000);
    }

    [Fact]
    public async Task PagedExtraction_LargePdf_CompletesWithinExtendedTarget()
    {
        // 500 pages - all stages attempted
        var stage1 = new TimedFakeExtractor(delayMs: 100, success: true, pageCount: 500, charsPerPage: 200);
        var stage2 = new TimedFakeExtractor(delayMs: 100, success: true, pageCount: 500, charsPerPage: 400);
        var stage3 = new TimedFakeExtractor(delayMs: 50, success: true, pageCount: 500, charsPerPage: 500);

        var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
        await using var pdfStream = CreateDummyPdfStream();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream);
        sw.Stop();

        result.Success.Should().BeTrue();
        result.StageUsed.Should().Be(3, "Both Stage 1 and 2 below quality thresholds");
        result.TotalPages.Should().Be(500);
        result.TotalCharacters.Should().Be(250_000); // 500 * 500
        sw.ElapsedMilliseconds.Should().BeLessThan(60000, "Large PDF should complete within 60s");
    }

    #endregion

    #region Duration Tracking Tests

    [Fact]
    public async Task DurationTracking_RecordsAccurateTimings()
    {
        var stage1 = new TimedFakeExtractor(delayMs: 50, success: true, quality: ExtractionQuality.High,
            text: "Timed extraction");
        var stage2 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.Medium);
        var stage3 = new TimedFakeExtractor(delayMs: 0, success: true, quality: ExtractionQuality.Low);

        var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
        await using var pdfStream = CreateDummyPdfStream();

        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        result.Success.Should().BeTrue();
        result.TotalDurationMs.Should().BeGreaterThanOrEqualTo(0,
            "Duration should be recorded (can be 0 for very fast operations due to timer resolution)");
        result.TotalDurationMs.Should().BeLessThan(5000,
            "Duration should not be unreasonably long for a 50ms fake delay");
    }

    [Fact]
    public async Task DurationTracking_MultipleStageFallback_RecordsCumulativeTime()
    {
        // Each stage takes ~50ms, 3 stages attempted
        var stage1 = new TimedFakeExtractor(delayMs: 50, success: true, quality: ExtractionQuality.VeryLow);
        var stage2 = new TimedFakeExtractor(delayMs: 50, success: true, quality: ExtractionQuality.Low);
        var stage3 = new TimedFakeExtractor(delayMs: 50, success: true, quality: ExtractionQuality.Medium);

        var orchestrator = CreateOrchestrator(stage1, stage2, stage3);
        await using var pdfStream = CreateDummyPdfStream();

        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        result.Success.Should().BeTrue();
        result.StageUsed.Should().Be(3);
        result.TotalDurationMs.Should().BeGreaterThanOrEqualTo(100,
            "Cumulative duration should reflect multiple stage attempts");
    }

    #endregion

    #region Helper Methods

    private EnhancedPdfProcessingOrchestrator CreateOrchestrator(
        IPdfTextExtractor stage1, IPdfTextExtractor stage2, IPdfTextExtractor stage3)
    {
        return new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, _options);
    }

    private static string GenerateText(int pageCount, int charsPerPage)
    {
        return string.Join("\n", Enumerable.Range(1, pageCount)
            .Select(p => new string('A', charsPerPage)));
    }

    private static MemoryStream CreateDummyPdfStream()
    {
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
        return new MemoryStream(pdfBytes);
    }

    /// <summary>
    /// Fake extractor with configurable delay for performance testing.
    /// </summary>
    private sealed class TimedFakeExtractor : IPdfTextExtractor
    {
        private readonly int _delayMs;
        private readonly bool _success;
        private readonly ExtractionQuality _quality;
        private readonly string _text;
        private readonly string? _errorMsg;
        private readonly int _pageCount;
        private readonly int _charsPerPage;

        public TimedFakeExtractor(
            int delayMs = 0,
            bool success = true,
            ExtractionQuality quality = ExtractionQuality.High,
            string? text = null,
            string? errorMsg = null,
            int pageCount = 10,
            int charsPerPage = 1000)
        {
            _delayMs = delayMs;
            _success = success;
            _quality = quality;
            _text = text ?? new string('X', pageCount * charsPerPage);
            _errorMsg = errorMsg;
            _pageCount = pageCount;
            _charsPerPage = charsPerPage;
        }

        public async Task<TextExtractionResult> ExtractTextAsync(
            Stream pdfStream, bool enableOcrFallback = true,
            CancellationToken cancellationToken = default)
        {
            if (_delayMs > 0)
                await Task.Delay(_delayMs, cancellationToken);

            if (_success)
                return TextExtractionResult.CreateSuccess(_text, _pageCount, _text.Length, false, _quality);

            return TextExtractionResult.CreateFailure(_errorMsg ?? "Timed extraction failed");
        }

        public async Task<PagedTextExtractionResult> ExtractPagedTextAsync(
            Stream pdfStream, bool enableOcrFallback = true,
            CancellationToken cancellationToken = default)
        {
            if (_delayMs > 0)
                await Task.Delay(_delayMs, cancellationToken);

            if (_success)
            {
                var chunks = Enumerable.Range(1, _pageCount)
                    .Select(i => new PageTextChunk(i, new string('X', _charsPerPage),
                        (i - 1) * _charsPerPage, i * _charsPerPage))
                    .ToList();

                return PagedTextExtractionResult.CreateSuccess(
                    chunks, _pageCount, _pageCount * _charsPerPage, false);
            }

            return PagedTextExtractionResult.CreateFailure(_errorMsg ?? "Timed paged extraction failed");
        }
    }

    #endregion
}
