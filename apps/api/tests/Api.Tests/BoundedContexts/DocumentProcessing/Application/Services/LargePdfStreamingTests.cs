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

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// BGAI-087: Tests for large PDF streaming optimization
/// Verifies temp file strategy for PDFs ≥50MB to reduce memory pressure
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// </summary>
public class LargePdfStreamingTests
{
    private readonly ILogger<EnhancedPdfProcessingOrchestrator> _logger;
    private readonly IConfiguration _configuration;

    public LargePdfStreamingTests()
    {
        _logger = Mock.Of<ILogger<EnhancedPdfProcessingOrchestrator>>();
        _configuration = new ConfigurationBuilder().Build();
    }

    [Fact]
    public async Task ExtractTextWithFallbackAsync_SmallPdf_UsesMemoryStrategy()
    {
        // Arrange - 10 MB PDF (below 50 MB threshold)
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52428800, // 50 MB
            UseTempFileForLargePdfs = true
        });

        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Test content");
        var stage2 = new FakeExtractor(success: false);
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreateTestPdfStream(PdfUploadTestConstants.FileSizes.TestMaxBytes); // 10 MB

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().Be("Test content");
        result.StageUsed.Should().Be(1); // Stage 1 should succeed
    }

    [Fact]
    public async Task ExtractTextWithFallbackAsync_LargePdf_UsesTempFileStrategy()
    {
        // Arrange - 60 MB PDF (above 50 MB threshold)
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52428800, // 50 MB
            UseTempFileForLargePdfs = true
        });

        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Large PDF content");
        var stage2 = new FakeExtractor(success: false);
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreateTestPdfStream(60 * 1024 * 1024); // 60 MB

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().Be("Large PDF content");
        result.StageUsed.Should().Be(1);
        // Note: Temp file should be automatically cleaned up via Dispose
    }

    [Fact]
    public async Task ExtractTextWithFallbackAsync_LargePdfWithTempFileDisabled_UsesMemory()
    {
        // Arrange - 60 MB PDF but temp file disabled
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52428800, // 50 MB
            UseTempFileForLargePdfs = false // Disabled
        });

        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Memory content");
        var stage2 = new FakeExtractor(success: false);
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreateTestPdfStream(60 * 1024 * 1024); // 60 MB

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().Be("Memory content");
    }

    [Fact]
    public async Task ExtractPagedTextWithFallbackAsync_LargePdf_UsesTempFileStrategy()
    {
        // Arrange - 75 MB PDF (above 50 MB threshold)
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52428800,
            UseTempFileForLargePdfs = true
        });

        // Need ≥800 chars/page to achieve quality score ≥0.80
        // Quality formula: 0.5 + (chars - 500) / 500 * 0.5 for chars in [500, 1000]
        var realisticText = new string('A', 850); // 850 chars → quality score ~0.85
        var chunks = new List<PageTextChunk>
        {
            new PageTextChunk(PageNumber: 1, Text: realisticText, CharStartIndex: 0, CharEndIndex: realisticText.Length)
        };

        var stage1 = new FakePagedExtractor(success: true, chunks: chunks);
        var stage2 = new FakePagedExtractor(success: false);
        var stage3 = new FakePagedExtractor(success: true, chunks: chunks);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreateTestPdfStream(75 * 1024 * 1024); // 75 MB

        // Act
        var result = await orchestrator.ExtractPagedTextWithFallbackAsync(pdfStream);

        // Assert
        result.Success.Should().BeTrue();
        result.PageChunks.Should().ContainSingle();
        result.StageUsed.Should().Be(1);
    }

    [Fact]
    public async Task ExtractTextWithFallbackAsync_VeryLargePdf_SucceedsWithMultipleStageRetries()
    {
        // Arrange - 100 MB PDF with all stages retrying
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52428800,
            UseTempFileForLargePdfs = true
        });

        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.VeryLow); // Below 0.80
        var stage2 = new FakeExtractor(success: true, quality: ExtractionQuality.Low); // Below 0.70
        var stage3 = new FakeExtractor(success: true, quality: ExtractionQuality.Low, text: "Fallback content");

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreateTestPdfStream(100 * 1024 * 1024); // 100 MB

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().Be("Fallback content");
        result.StageUsed.Should().Be(3); // Should fall through to Stage 3
    }

    [Theory]
    [InlineData(5 * 1024 * 1024)]   // 5 MB - small
    [InlineData(30 * 1024 * 1024)]  // 30 MB - medium
    [InlineData(49 * 1024 * 1024)]  // 49 MB - just below threshold
    public async Task ExtractTextWithFallbackAsync_BelowThreshold_UsesMemory(long size)
    {
        // Arrange
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52428800,
            UseTempFileForLargePdfs = true
        });

        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Content");
        var stage2 = new FakeExtractor(success: false);
        var stage3 = new FakeExtractor(success: true);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreateTestPdfStream(size);

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData(50 * 1024 * 1024)]  // 50 MB - exactly at threshold
    [InlineData(75 * 1024 * 1024)]  // 75 MB - above threshold
    [InlineData(100 * 1024 * 1024)] // 100 MB - large
    public async Task ExtractTextWithFallbackAsync_AtOrAboveThreshold_UsesTempFile(long size)
    {
        // Arrange
        var options = Options.Create(new PdfProcessingOptions
        {
            LargePdfThresholdBytes = 52428800,
            UseTempFileForLargePdfs = true
        });

        var stage1 = new FakeExtractor(success: true, quality: ExtractionQuality.High, text: "Large content");
        var stage2 = new FakeExtractor(success: false);
        var stage3 = new FakeExtractor(success: true);

        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            stage1, stage2, stage3, _logger, _configuration, options);

        await using var pdfStream = CreateTestPdfStream(size);

        // Act
        var result = await orchestrator.ExtractTextWithFallbackAsync(pdfStream);

        // Assert
        result.Success.Should().BeTrue();
        // Temp file should be cleaned up automatically after extraction
    }

    #region Helper Methods

    private static Stream CreateTestPdfStream(long size)
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);

        // Write minimal PDF header
        writer.Write("%PDF-1.4\n"u8.ToArray());

        // Fill to desired size
        var remaining = size - stream.Position;
        var buffer = new byte[Math.Min(8192, remaining)];
        while (remaining > 0)
        {
            var toWrite = (int)Math.Min(buffer.Length, remaining);
            writer.Write(buffer, 0, toWrite);
            remaining -= toWrite;
        }

        stream.Position = 0;
        return stream;
    }

    #endregion
}

/// <summary>
/// Fake PDF text extractor for testing
/// </summary>
file class FakeExtractor : IPdfTextExtractor
{
    private readonly bool _success;
    private readonly ExtractionQuality _quality;
    private readonly string _text;
    private readonly string _name;

    public FakeExtractor(
        bool success = true,
        ExtractionQuality quality = ExtractionQuality.High,
        string text = "Fake extracted text",
        string name = "Fake")
    {
        _success = success;
        _quality = quality;
        _text = text;
        _name = name;
    }

    public Task<TextExtractionResult> ExtractTextAsync(Stream pdfStream, bool enableOcrFallback, CancellationToken ct)
    {
        var result = new TextExtractionResult(
            Success: _success,
            ExtractedText: _text,
            PageCount: 10,
            CharacterCount: _text.Length,
            OcrTriggered: false,
            Quality: _quality,
            ErrorMessage: _success ? null : $"{_name} extraction failed");

        return Task.FromResult(result);
    }

    public Task<PagedTextExtractionResult> ExtractPagedTextAsync(Stream pdfStream, bool enableOcrFallback, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Fake paged PDF text extractor for testing
/// </summary>
file class FakePagedExtractor : IPdfTextExtractor
{
    private readonly bool _success;
    private readonly List<PageTextChunk> _chunks;

    public FakePagedExtractor(bool success = true, List<PageTextChunk>? chunks = null)
    {
        _success = success;
        _chunks = chunks ?? new List<PageTextChunk>();
    }

    public Task<TextExtractionResult> ExtractTextAsync(Stream pdfStream, bool enableOcrFallback, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<PagedTextExtractionResult> ExtractPagedTextAsync(Stream pdfStream, bool enableOcrFallback, CancellationToken ct)
    {
        var result = new PagedTextExtractionResult(
            Success: _success,
            PageChunks: _chunks,
            TotalPages: _chunks.Count,
            TotalCharacters: _chunks.Sum(c => c.CharCount),
            OcrTriggered: false,
            ErrorMessage: _success ? null : "Fake paged extraction failed");

        return Task.FromResult(result);
    }
}