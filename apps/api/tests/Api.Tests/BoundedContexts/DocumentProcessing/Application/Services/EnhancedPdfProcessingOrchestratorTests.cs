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
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

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
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

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
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

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
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

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
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

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
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

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
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

        // Assert - Should fallback to Stage 2 after catching exception
        Assert.True(result.Success);
        Assert.Equal(2, result.StageUsed);
        Assert.Equal("Stage2 recovered", result.ExtractedText);
    }

    [Fact]
    public async Task Test07_PagedExtraction_Stage1Success()
    {
        // Arrange - Paged extraction with Stage 1 success (high quality chars/page)
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, pageCount: 10, charsPerPage: 1200); // 0.85 ≥ 0.80
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, pageCount: 10, charsPerPage: 600);
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, pageCount: 10, charsPerPage: 300);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.StageUsed);
        Assert.Equal(10, result.PageChunks.Count);
        Assert.True(result.TotalDurationMs >= 0); // Can be 0 for very fast operations

        // Only Stage 1 called
        Assert.Equal(1, stage1.PagedCallCount);
        Assert.Equal(0, stage2.PagedCallCount);
        Assert.Equal(0, stage3.PagedCallCount);
    }

    [Fact]
    public async Task Test08_PagedExtraction_FallbackToStage3()
    {
        // Arrange - Stages 1-2 fail paged extraction, Stage 3 succeeds (best effort)
        var stage1 = new FakeExtractor(success: false, errorMsg: "Stage1 paged failed");
        var stage2 = new FakeExtractor(success: false, errorMsg: "Stage2 paged failed");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, pageCount: 5, charsPerPage: 200);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

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
    public async Task Test09_PagedExtraction_HonorsQualityThreshold()
    {
        // Arrange - Test quality-based fallback with linear interpolation
        // Stage 1: 400 cpp → score 0.40 < 0.80 → falls back
        // Stage 2: 850 cpp → score 0.85 ≥ 0.70 → accepts
        var stage1 = new FakeExtractor(success: true, pageCount: 10, charsPerPage: 400); // 0.40 < 0.80
        var stage2 = new FakeExtractor(success: true, pageCount: 10, charsPerPage: 850); // 0.85 ≥ 0.70
        var stage3 = new FakeExtractor(success: true, pageCount: 10, charsPerPage: 100); // 0.10

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.StageUsed); // Should use Stage 2 (Stage 1 below threshold)
        Assert.Equal("SmolDocling", result.StageName);
        Assert.Equal(10, result.PageChunks.Count);
        Assert.Equal(8500, result.TotalCharacters); // 10 pages * 850 chars/page

        // Verify fallback occurred
        Assert.Equal(1, stage1.PagedCallCount); // Stage 1 called but rejected
        Assert.Equal(1, stage2.PagedCallCount); // Stage 2 called and accepted
        Assert.Equal(0, stage3.PagedCallCount); // Stage 3 NOT called
    }

    [Fact]
    public async Task Test10_PagedExtraction_VeryLowQualityFallsBackToStage3()
    {
        // Arrange - All stages low quality with linear interpolation
        // Stage 1: 200 cpp → 0.20 < 0.80 → rejects
        // Stage 2: 400 cpp → 0.40 < 0.70 → rejects
        // Stage 3: 300 cpp → accepts (best effort, no threshold)
        var stage1 = new FakeExtractor(success: true, pageCount: 5, charsPerPage: 200); // 0.20 < 0.80
        var stage2 = new FakeExtractor(success: true, pageCount: 5, charsPerPage: 400); // 0.40 < 0.70
        var stage3 = new FakeExtractor(success: true, pageCount: 5, charsPerPage: 300); // Best effort

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.StageUsed); // Should use Stage 3 (all others below threshold)
        Assert.Equal("Docnet", result.StageName);
        Assert.Equal(5, result.PageChunks.Count);
        Assert.Equal(1500, result.TotalCharacters); // 5 pages * 300 chars/page

        // Verify all stages attempted
        Assert.Equal(1, stage1.PagedCallCount);
        Assert.Equal(1, stage2.PagedCallCount);
        Assert.Equal(1, stage3.PagedCallCount);
    }

    [Fact]
    public async Task Test11_PagedExtraction_HighQualityStage1Accepted()
    {
        // Arrange - Stage 1 at threshold boundary (800 cpp → 0.80)
        // Stage 1: 800 cpp → score 0.80 ≥ 0.80 → accepts immediately (exactly at threshold)
        var stage1 = new FakeExtractor(success: true, pageCount: 8, charsPerPage: 800); // 0.80 ≥ 0.80
        var stage2 = new FakeExtractor(success: true, pageCount: 8, charsPerPage: 700);
        var stage3 = new FakeExtractor(success: true, pageCount: 8, charsPerPage: 500);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration);

        await using var pdfStream = CreateDummyPdfStream();

        // Act
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.StageUsed); // Stage 1 accepted immediately
        Assert.Equal("Unstructured", result.StageName);
        Assert.Equal(8, result.PageChunks.Count);
        Assert.Equal(6400, result.TotalCharacters); // 8 pages * 800 chars/page

        // Verify only Stage 1 called
        Assert.Equal(1, stage1.PagedCallCount);
        Assert.Equal(0, stage2.PagedCallCount); // NOT called
        Assert.Equal(0, stage3.PagedCallCount); // NOT called
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
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

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
            var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

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

    #region BGAI-088: Defense in Depth - File Size Validation Tests

    [Theory]
    [InlineData(52428800, true)]     // 50 MB - under limit (50 * 1024 * 1024)
    [InlineData(104857600, true)]    // 100 MB - at limit (boundary) (100 * 1024 * 1024)
    [InlineData(105906176, false)]   // 101 MB - exceeds limit (101 * 1024 * 1024)
    public async Task Test11_FileSizeLimit_ReturnsExpected(long size, bool shouldSucceed)
    {
        // Arrange - Create fake extractors (won't be called for oversized PDFs)
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Should not extract");
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, text: "Should not extract");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Should not extract");

        // Configuration with 100 MB limit
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfProcessing:MaxFileSizeBytes"] = "104857600" // 100 MB
            })
            .Build();

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, config);

        await using var pdfStream = CreateTestPdfStream(size);

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        Assert.Equal(shouldSucceed, result.Success);

        if (!shouldSucceed)
        {
            // Rejected due to size
            Assert.Contains("exceeds maximum", result.ErrorMessage);
            Assert.Equal(0, result.StageUsed); // No stage was used
            Assert.Equal("None", result.StageName);
            Assert.Equal(0, result.CharacterCount);

            // Verify extractors were NOT called (defense in depth prevented processing)
            Assert.Equal(0, stage1.CallCount);
            Assert.Equal(0, stage2.CallCount);
            Assert.Equal(0, stage3.CallCount);
        }
        else
        {
            // Accepted and processed
            Assert.True(result.Success);
            Assert.True(result.StageUsed > 0, "At least one stage should have processed the PDF");
        }
    }

    [Fact]
    public async Task Test12_NonSeekableStream_SkipsSizeCheck()
    {
        // Arrange - Non-seekable stream (size check should be skipped gracefully)
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Extracted successfully");
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, text: "Stage2");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Stage3");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfProcessing:MaxFileSizeBytes"] = "100" // Very low limit (100 bytes)
            })
            .Build();

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, config);

        // Create non-seekable stream wrapper
        await using var baseStream = CreateDummyPdfStream();
        await using var nonSeekableStream = new NonSeekableStreamWrapper(baseStream);

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(nonSeekableStream, ct: TestCancellationToken);

        // Assert - Should process successfully even though limit is very low
        Assert.True(result.Success, "Non-seekable stream should skip size check and process normally");
        Assert.Equal("Extracted successfully", result.ExtractedText);
        Assert.Equal(1, stage1.CallCount); // Stage 1 should have been called
    }

    [Fact]
    public async Task Test13_DefaultMaxSize_WhenConfigMissing()
    {
        // Arrange - No configuration (should use default 100 MB)
        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Extracted");
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Medium, text: "Stage2");
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Stage3");

        var emptyConfig = new ConfigurationBuilder().Build(); // No PdfProcessing:MaxFileSizeBytes

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, emptyConfig);

        // 110 MB stream - should exceed default 100 MB
        await using var pdfStream = CreateTestPdfStream(115343360); // 110 * 1024 * 1024

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream, ct: TestCancellationToken);

        // Assert - Should reject (default is 100 MB)
        Assert.False(result.Success);
        Assert.Contains("exceeds maximum", result.ErrorMessage);
        Assert.Contains("115", result.ErrorMessage); // Should show actual size (115343360 bytes = 110 MB = 115.3 in decimal MB)
    }

    #endregion

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
        private readonly int _charsPerPage;

        public int CallCount { get; private set; }
        public int PagedCallCount { get; private set; }

        public FakeExtractor(
            bool success = true,
            ExtractionQuality quality = ExtractionQuality.High,
            string text = "Fake extracted text",
            string? errorMsg = null,
            bool throwException = false,
            int pageCount = 10,
            int charsPerPage = 100)
        {
            _success = success;
            _quality = quality;
            _text = text;
            _errorMsg = errorMsg;
            _throwException = throwException;
            _pageCount = pageCount;
            _charsPerPage = charsPerPage;
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
                // Generate chunks with configurable chars per page (for quality testing)
                var chunks = Enumerable.Range(1, _pageCount)
                    .Select(i => new PageTextChunk(
                        PageNumber: i,
                        Text: new string('X', _charsPerPage), // Generate text of exact length
                        CharStartIndex: (i - 1) * _charsPerPage,
                        CharEndIndex: i * _charsPerPage))
                    .ToList();

                return Task.FromResult(PagedTextExtractionResult.CreateSuccess(
                    pageChunks: chunks,
                    totalPages: _pageCount,
                    totalCharacters: _pageCount * _charsPerPage,
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

    private static MemoryStream CreateTestPdfStream(long size)
    {
        // Create PDF with specific size for testing file size limits
        var pdfBytes = new byte[size];
        // Minimal PDF header at start
        pdfBytes[0] = 0x25; // %
        pdfBytes[1] = 0x50; // P
        pdfBytes[2] = 0x44; // D
        pdfBytes[3] = 0x46; // F
        pdfBytes[4] = 0x2D; // -
        pdfBytes[5] = 0x31; // 1
        pdfBytes[6] = 0x2E; // .
        pdfBytes[7] = 0x34; // 4
        return new MemoryStream(pdfBytes);
    }

    /// <summary>
    /// Wrapper to make a stream non-seekable for testing
    /// </summary>
    private class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _innerStream;

        public NonSeekableStreamWrapper(Stream innerStream)
        {
            _innerStream = innerStream;
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => false; // Force non-seekable
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException("Non-seekable stream");
        public override long Position
        {
            get => throw new NotSupportedException("Non-seekable stream");
            set => throw new NotSupportedException("Non-seekable stream");
        }

        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Non-seekable stream");
        public override void SetLength(long value) => throw new NotSupportedException("Non-seekable stream");
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Non-seekable stream");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _innerStream.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    #endregion
}
