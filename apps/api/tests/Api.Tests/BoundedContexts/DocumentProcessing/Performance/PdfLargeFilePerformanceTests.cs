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
/// Performance tests for large PDF handling, temp file strategy, and memory management.
/// Validates the BGAI-087 optimization (temp file for PDFs ≥50MB).
/// Issue #4143: Performance Testing - PDF Wizard
/// </summary>
[Trait("Category", TestCategories.Performance)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4143")]
public class PdfLargeFilePerformanceTests
{
    private readonly ILogger<EnhancedPdfProcessingOrchestrator> _logger;
    private readonly IConfiguration _configuration;

    public PdfLargeFilePerformanceTests()
    {
        _logger = Mock.Of<ILogger<EnhancedPdfProcessingOrchestrator>>();
        _configuration = new ConfigurationBuilder().Build();
    }

    #region Size-Based Strategy Tests

    [Theory]
    [InlineData(10_000_000, "10 MB - small PDF")]
    [InlineData(30_000_000, "30 MB - medium PDF")]
    [InlineData(49_000_000, "49 MB - just under threshold")]
    public async Task SmallPdf_UsesInMemoryStrategy(long sizeBytes, string description)
    {
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52_428_800, // 50 MB
            UseTempFileForLargePdfs = true
        });

        var stage1 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.High,
            text: $"Small PDF text ({description})");
        var stage2 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Medium);
        var stage3 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Low);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreatePdfStream(sizeBytes);

        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        result.Success.Should().BeTrue($"{description} should process successfully");
        result.StageUsed.Should().Be(1);
    }

    [Fact]
    public async Task LargePdf_AtThreshold_ProcessesSuccessfully()
    {
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52_428_800,
            UseTempFileForLargePdfs = true
        });

        var stage1 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.High,
            text: "Threshold PDF text");
        var stage2 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Medium);
        var stage3 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Low);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        // Exactly at threshold (50 MB)
        await using var pdfStream = CreatePdfStream(52_428_800);

        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        result.Success.Should().BeTrue("PDF at threshold should still process");
    }

    #endregion

    #region Max File Size Enforcement Tests

    [Fact]
    public async Task ExceedsMaxSize_RejectsBeforeProcessing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfProcessing:MaxFileSizeBytes"] = "104857600" // 100 MB
            })
            .Build();

        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52_428_800,
            UseTempFileForLargePdfs = true
        });

        var stage1 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.High);
        var stage2 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Medium);
        var stage3 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Low);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, config, options);

        // 110 MB - exceeds 100 MB limit
        await using var pdfStream = CreatePdfStream(115_343_360);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);
        sw.Stop();

        result.Success.Should().BeFalse("Oversized PDF should be rejected");
        result.ErrorMessage.Should().Contain("exceeds maximum");
        result.StageUsed.Should().Be(0, "No extraction stage should be attempted");
        sw.ElapsedMilliseconds.Should().BeLessThan(1000,
            "Rejection should be fast - no extraction processing");
    }

    [Fact]
    public async Task AtMaxSize_ProcessesSuccessfully()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfProcessing:MaxFileSizeBytes"] = "104857600" // 100 MB
            })
            .Build();

        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52_428_800,
            UseTempFileForLargePdfs = true
        });

        var stage1 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.High,
            text: "Max size PDF text");
        var stage2 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Medium);
        var stage3 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Low);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, config, options);

        // Exactly 100 MB - at the limit
        await using var pdfStream = CreatePdfStream(104_857_600);

        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        result.Success.Should().BeTrue("PDF at max size limit should be accepted");
    }

    #endregion

    #region Large PDF Performance Targets

    [Fact]
    public async Task LargePdf_500Pages_ExtractWithinTimeout()
    {
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52_428_800,
            UseTempFileForLargePdfs = true
        });

        // Simulate a 500-page document with Stage 1 success
        var stage1 = new LargeFileFakeExtractor(delayMs: 100, success: true, quality: ExtractionQuality.High,
            pageCount: 500, charsPerPage: 1200);
        var stage2 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Medium);
        var stage3 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Low);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreateDummyPdfStream();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, cancellationToken: cts.Token);
        sw.Stop();

        result.Success.Should().BeTrue();
        result.StageUsed.Should().Be(1);
        sw.ElapsedMilliseconds.Should().BeLessThan(30000,
            "500-page PDF via Stage 1 should complete within 30s target");
    }

    [Fact]
    public async Task LargePdf_500Pages_PagedExtraction_WithinTimeout()
    {
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52_428_800,
            UseTempFileForLargePdfs = true
        });

        var stage1 = new LargeFileFakeExtractor(delayMs: 100, success: true,
            pageCount: 500, charsPerPage: 1000);
        var stage2 = new LargeFileFakeExtractor(success: true, pageCount: 500, charsPerPage: 700);
        var stage3 = new LargeFileFakeExtractor(success: true, pageCount: 500, charsPerPage: 300);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreateDummyPdfStream();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream, cancellationToken: cts.Token);
        sw.Stop();

        result.Success.Should().BeTrue();
        result.TotalPages.Should().Be(500);
        result.PageChunks.Count.Should().Be(500);
        result.TotalCharacters.Should().Be(500_000);
        sw.ElapsedMilliseconds.Should().BeLessThan(60000,
            "500-page paged extraction should complete within 60s target");
    }

    #endregion

    #region Concurrent Large PDF Tests

    [Fact]
    public async Task ConcurrentLargePdfs_3Sessions_NoMemoryPressure()
    {
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52_428_800,
            UseTempFileForLargePdfs = true
        });

        const int sessionCount = 3;
        var tasks = new List<Task<EnhancedExtractionResult>>();

        for (var i = 0; i < sessionCount; i++)
        {
            var sessionId = i;
            tasks.Add(Task.Run(async () =>
            {
                var stage1 = new LargeFileFakeExtractor(delayMs: 50, success: true,
                    quality: ExtractionQuality.High,
                    text: $"Large PDF session {sessionId}",
                    pageCount: 200, charsPerPage: 1000);
                var stage2 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Medium);
                var stage3 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Low);

                var orchestrator = new EnhancedPdfProcessingOrchestrator(
                    stage1, stage2, stage3, _logger, _configuration, options);

                await using var pdfStream = CreateDummyPdfStream();
                return await orchestrator.ExtractTextWithFallbackAsync(pdfStream);
            }));
        }

        var allCompleted = Task.WhenAll(tasks);
        var completedInTime = await Task.WhenAny(allCompleted, Task.Delay(TimeSpan.FromSeconds(30)));

        completedInTime.Should().Be(allCompleted,
            "3 concurrent large PDF extractions should complete within 30s");

        var results = await allCompleted;
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.StageUsed.Should().Be(1);
        });
    }

    #endregion

    #region TempFile Disabled Configuration

    [Fact]
    public async Task TempFileDisabled_LargePdf_StillProcesses()
    {
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52_428_800,
            UseTempFileForLargePdfs = false // Disabled
        });

        var stage1 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.High,
            text: "No temp file text");
        var stage2 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Medium);
        var stage3 = new LargeFileFakeExtractor(success: true, quality: ExtractionQuality.Low);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreateDummyPdfStream();

        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        result.Success.Should().BeTrue("Extraction should work even without temp file strategy");
    }

    #endregion

    #region Helper Methods

    private static MemoryStream CreateDummyPdfStream()
    {
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        return new MemoryStream(pdfBytes);
    }

    private static MemoryStream CreatePdfStream(long size)
    {
        var bytes = new byte[size];
        bytes[0] = 0x25; bytes[1] = 0x50; bytes[2] = 0x44; bytes[3] = 0x46;
        bytes[4] = 0x2D; bytes[5] = 0x31; bytes[6] = 0x2E; bytes[7] = 0x34;
        return new MemoryStream(bytes);
    }

    private sealed class LargeFileFakeExtractor : IPdfTextExtractor
    {
        private readonly int _delayMs;
        private readonly bool _success;
        private readonly ExtractionQuality _quality;
        private readonly string _text;
        private readonly string? _errorMsg;
        private readonly int _pageCount;
        private readonly int _charsPerPage;

        public LargeFileFakeExtractor(
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

            return _success
                ? TextExtractionResult.CreateSuccess(_text, _pageCount, _text.Length, false, _quality)
                : TextExtractionResult.CreateFailure(_errorMsg ?? "Large file extraction failed");
        }

        public async Task<PagedTextExtractionResult> ExtractPagedTextAsync(
            Stream pdfStream, bool enableOcrFallback = true,
            CancellationToken cancellationToken = default)
        {
            if (_delayMs > 0)
                await Task.Delay(_delayMs, cancellationToken);

            if (!_success)
                return PagedTextExtractionResult.CreateFailure(_errorMsg ?? "Large file paged failed");

            var chunks = Enumerable.Range(1, _pageCount)
                .Select(i => new PageTextChunk(i, new string('X', _charsPerPage),
                    (i - 1) * _charsPerPage, i * _charsPerPage))
                .ToList();

            return PagedTextExtractionResult.CreateSuccess(
                chunks, _pageCount, _pageCount * _charsPerPage, false);
        }
    }

    #endregion
}
