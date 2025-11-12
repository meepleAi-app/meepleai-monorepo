using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Unit tests for EnhancedPdfProcessingOrchestrator 3-stage fallback logic
/// </summary>
/// <remarks>
/// Issue #949: BGAI-010 - Tests quality-based fallback, performance tracking, error handling
/// Architecture: Stage1(Unstructured ≥0.80) → Stage2(SmolDocling ≥0.70) → Stage3(Docnet)
/// </remarks>
public class EnhancedPdfProcessingOrchestratorTests
{
    private readonly ILogger<EnhancedPdfProcessingOrchestrator> _logger;
    private readonly IConfiguration _configuration;

    public EnhancedPdfProcessingOrchestratorTests()
    {
        _logger = Mock.Of<ILogger<EnhancedPdfProcessingOrchestrator>>();
        _configuration = new ConfigurationBuilder().Build();
    }

    [Fact]
    public async Task Test01_Stage1HighQuality_ReturnsStage1Result()
    {
        // Arrange - Stage 1 succeeds with High quality (0.85 ≥ 0.80)
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Stage1 text");
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, text: "Stage2 text");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Stage3 text");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.StageUsed);
        Assert.Equal("Unstructured", result.StageName); // Orchestrator names stages
        Assert.Equal("Stage1 text", result.ExtractedText);
        Assert.Equal(ExtractionQuality.High, result.Quality);
        Assert.True(result.TotalDurationMs >= 0); // Can be 0 for very fast operations

        // Verify only Stage 1 was called
        Assert.Equal(1, stage1.CallCount);
        Assert.Equal(0, stage2.CallCount); // Should NOT be called
        Assert.Equal(0, stage3.CallCount); // Should NOT be called
    }

    [Fact]
    public async Task Test02_Stage1LowQuality_FallsBackToStage2()
    {
        // Arrange - Stage 1 Low quality (0.50 < 0.80), Stage 2 Medium (0.70 ≥ 0.70)
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Stage1 low");
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, text: "Stage2 good");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Stage3");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.StageUsed);
        Assert.Equal("Stage2 good", result.ExtractedText);
        Assert.Equal(ExtractionQuality.Medium, result.Quality);

        // Verify Stage 1 and 2 called, Stage 3 NOT called
        Assert.Equal(1, stage1.CallCount);
        Assert.Equal(1, stage2.CallCount);
        Assert.Equal(0, stage3.CallCount);
    }

    [Fact]
    public async Task Test03_Stage1And2Fail_FallsBackToStage3()
    {
        // Arrange - Stages 1-2 fail, Stage 3 succeeds (best effort)
        var stage1 = new FakeExtractor(success: false, quality: ExtractionQuality.VeryLow, text: "", errorMsg: "Stage1 unavailable");
        var stage2 = new FakeExtractor(success: false, quality: ExtractionQuality.VeryLow, text: "", errorMsg: "Stage2 timeout");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Stage3 fallback");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.StageUsed);
        Assert.Equal("Stage3 fallback", result.ExtractedText);

        // Verify all 3 stages attempted
        Assert.Equal(1, stage1.CallCount);
        Assert.Equal(1, stage2.CallCount);
        Assert.Equal(1, stage3.CallCount);
    }

    [Fact]
    public async Task Test04_Stage2LowQuality_FallsBackToStage3()
    {
        // Arrange - Stage 1 Low (0.50 < 0.80), Stage 2 Low (0.50 < 0.70), Stage 3 Medium
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Stage1");
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Stage2");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, text: "Stage3");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.StageUsed);

        // All 3 stages attempted due to quality thresholds
        Assert.Equal(1, stage1.CallCount);
        Assert.Equal(1, stage2.CallCount);
        Assert.Equal(1, stage3.CallCount);
    }

    [Fact]
    public async Task Test05_AllStagesFail_ReturnsStage3FailureResult()
    {
        // Arrange - All stages fail
        var stage1 = new FakeExtractor(success: false, quality: ExtractionQuality.VeryLow, text: "", errorMsg: "Stage1 error");
        var stage2 = new FakeExtractor(success: false, quality: ExtractionQuality.VeryLow, text: "", errorMsg: "Stage2 error");
        var stage3 = new FakeExtractor(success: false, quality: ExtractionQuality.VeryLow, text: "", errorMsg: "Stage3 error");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert - Returns Stage 3 result (even if failed)
        Assert.False(result.Success);
        Assert.Equal(3, result.StageUsed);
        Assert.Contains("Stage3 error", result.ErrorMessage);

        // All 3 stages attempted
        Assert.Equal(1, stage1.CallCount);
        Assert.Equal(1, stage2.CallCount);
        Assert.Equal(1, stage3.CallCount);
    }

    [Fact]
    public async Task Test06_Stage1Exception_CatchesAndFallsBackToStage2()
    {
        // Arrange - Stage 1 throws exception, Stage 2 succeeds
        var stage1 = new FakeExtractor(throwException: true);
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Stage2 recovered");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Stage3");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert - Should fallback to Stage 2 after catching exception
        Assert.True(result.Success);
        Assert.Equal(2, result.StageUsed);
        Assert.Equal("Stage2 recovered", result.ExtractedText);
    }

    [Fact]
    public async Task Test07_PagedExtraction_Stage1Success()
    {
        // Arrange - Paged extraction with Stage 1 success
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, pageCount: 10);
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, pageCount: 10);
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, pageCount: 10);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.StageUsed);
        Assert.Equal(10, result.PageChunks.Count);
        Assert.True(result.TotalDurationMs > 0);

        // Only Stage 1 called
        Assert.Equal(1, stage1.PagedCallCount);
        Assert.Equal(0, stage2.PagedCallCount);
        Assert.Equal(0, stage3.PagedCallCount);
    }

    [Fact]
    public async Task Test08_PagedExtraction_FallbackToStage3()
    {
        // Arrange - Stages 1-2 fail paged extraction, Stage 3 succeeds
        var stage1 = new FakeExtractor(success: false, errorMsg: "Stage1 paged failed");
        var stage2 = new FakeExtractor(success: false, errorMsg: "Stage2 paged failed");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, pageCount: 5);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.StageUsed);
        Assert.Equal(5, result.PageChunks.Count);

        // All 3 stages attempted
        Assert.Equal(1, stage1.PagedCallCount);
        Assert.Equal(1, stage2.PagedCallCount);
        Assert.Equal(1, stage3.PagedCallCount);
    }

    [Fact]
    public async Task Test09_PerformanceTracking_RecordsDuration()
    {
        // Arrange
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Fast extraction");
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, text: "Medium");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Slow");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert - Performance tracking
        Assert.True(result.TotalDurationMs >= 0, "Total duration should be recorded (can be 0 for fast ops)");
        Assert.True(result.TotalDurationMs < 5000, "Should complete quickly for fake extractors");
        Assert.NotNull(result.StageName);
    }

    [Fact]
    public async Task Test10_QualityMapping_CorrectThresholds()
    {
        // Test different quality levels and their threshold behavior
        var testCases = new[]
        {
            (ExtractionQuality.High, 0.85, 0.80, true),      // 0.85 ≥ 0.80 → Stage 1 accepts
            (ExtractionQuality.Medium, 0.70, 0.80, false),   // 0.70 < 0.80 → Stage 1 rejects, Stage 2 accepts (0.70 ≥ 0.70)
            (ExtractionQuality.Low, 0.50, 0.80, false),      // 0.50 < 0.80 and 0.50 < 0.70 → Both reject
            (ExtractionQuality.VeryLow, 0.25, 0.80, false)   // 0.25 < all thresholds → All reject
        };

        foreach (var (quality, expectedScore, threshold, shouldAccept) in testCases)
        {
            var stage1 = new FakeExtractor(success: true, quality: quality, text: $"Quality {quality}");
            var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Stage2");
            var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, text: "Stage3");

            var orchestrator = new EnhancedPdfProcessingOrchestrator(
                stage1, stage2, stage3, _logger, _configuration);

            await using var pdfStream = CreateDummyPdfStream();
            var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

            if (shouldAccept)
            {
                Assert.Equal(1, result.StageUsed); // Stage 1 accepted
            }
            else
            {
                Assert.True(result.StageUsed > 1, $"Quality {quality} should fallback (score {expectedScore} < {threshold})");
            }
        }
    }

    #region Helper: Fake Extractor

    /// <summary>
    /// Fake PDF extractor for testing orchestrator logic without real HTTP calls
    /// Implements IPdfTextExtractor directly to avoid virtual method issues
    /// </summary>
    private class FakeExtractor : IPdfTextExtractor
    {
        private readonly bool _success;
        private readonly ExtractionQuality _quality;
        private readonly string _text;
        private readonly string? _errorMsg;
        private readonly bool _throwException;
        private readonly int _pageCount;

        public int CallCount { get; private set; }
        public int PagedCallCount { get; private set; }

        public FakeExtractor(
            bool success = true,
            ExtractionQuality quality = ExtractionQuality.High,
            string text = "Fake extracted text",
            string? errorMsg = null,
            bool throwException = false,
            int pageCount = 10)
        {
            _success = success;
            _quality = quality;
            _text = text;
            _errorMsg = errorMsg;
            _throwException = throwException;
            _pageCount = pageCount;
        }

        public Task<TextExtractionResult> ExtractTextAsync(
            Stream pdfStream,
            bool enableOcrFallback = true,
            CancellationToken ct = default)
        {
            CallCount++;

            if (_throwException)
            {
                throw new HttpRequestException("Fake service unavailable");
            }

            if (_success)
            {
                return Task.FromResult(TextExtractionResult.CreateSuccess(
                    extractedText: _text,
                    pageCount: _pageCount,
                    characterCount: _text.Length,
                    ocrTriggered: false,
                    quality: _quality));
            }

            return Task.FromResult(TextExtractionResult.CreateFailure(_errorMsg ?? "Fake extraction failed"));
        }

        public Task<PagedTextExtractionResult> ExtractPagedTextAsync(
            Stream pdfStream,
            bool enableOcrFallback = true,
            CancellationToken ct = default)
        {
            PagedCallCount++;

            if (_throwException)
            {
                throw new HttpRequestException("Fake service unavailable");
            }

            if (_success)
            {
                var chunks = Enumerable.Range(1, _pageCount)
                    .Select(i => new PageTextChunk(
                        PageNumber: i,
                        Text: $"Page {i} content",
                        CharStartIndex: (i - 1) * 100,
                        CharEndIndex: i * 100))
                    .ToList();

                return Task.FromResult(PagedTextExtractionResult.CreateSuccess(
                    pageChunks: chunks,
                    totalPages: _pageCount,
                    totalCharacters: _pageCount * 100,
                    ocrTriggered: false));
            }

            return Task.FromResult(PagedTextExtractionResult.CreateFailure(_errorMsg ?? "Fake paged extraction failed"));
        }
    }

    #endregion

    #region Helper Methods

    private static MemoryStream CreateDummyPdfStream()
    {
        // Minimal PDF header
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
        return new MemoryStream(pdfBytes);
    }

    #endregion
}
