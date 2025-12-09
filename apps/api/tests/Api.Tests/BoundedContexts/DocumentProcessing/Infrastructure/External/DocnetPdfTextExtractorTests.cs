using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Tests for DocnetPdfTextExtractor.
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DocnetPdfTextExtractorTests : IDisposable
{
    private readonly Mock<ILogger<DocnetPdfTextExtractor>> _mockLogger;
    private readonly PdfTextProcessingDomainService _domainService;
    private readonly Mock<IOcrService> _mockOcrService;
    private readonly DocnetPdfTextExtractor _sut;
    private readonly List<string> _tempFilesToCleanup;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public DocnetPdfTextExtractorTests()
    {
        _mockLogger = new Mock<ILogger<DocnetPdfTextExtractor>>();

        // Setup configuration
        var configData = new Dictionary<string, string?>
        {
            { "PdfExtraction:Ocr:ThresholdCharsPerPage", "50" } // Lower threshold for testing
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _domainService = new PdfTextProcessingDomainService(configuration);
        _mockOcrService = new Mock<IOcrService>();

        _sut = new DocnetPdfTextExtractor(
            _mockLogger.Object,
            _domainService,
            _mockOcrService.Object);

        _tempFilesToCleanup = new List<string>();
    }

    public void Dispose()
    {
        // Cleanup temp files
        foreach (var file in _tempFilesToCleanup)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
    [Fact]
    public async Task ExtractTextAsync_ValidPdf_ReturnsSuccess()
    {
        // Arrange: Create a simple test PDF
        var pdfStream = CreateSimpleTestPdfStream();

        // Act
        var result = await _sut.ExtractTextAsync(pdfStream, enableOcrFallback: false, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().NotBeEmpty();
        result.PageCount.Should().BeGreaterThan(0);
        result.OcrTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractTextAsync_EmptyStream_ReturnsFailure()
    {
        // Arrange: Empty stream
        var emptyStream = new MemoryStream();

        // Act
        var result = await _sut.ExtractTextAsync(emptyStream, enableOcrFallback: false, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractTextAsync_LowQualityPdf_TriggersOcr()
    {
        // Arrange: PDF with very little text (simulated by creating minimal content)
        var pdfStream = CreateMinimalTextPdfStream();

        // Setup OCR to return better text
        _mockOcrService
            .Setup(ocr => ocr.ExtractTextFromPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                Success = true,
                ExtractedText = new string('a', 500), // 500 chars = good OCR result
                PageCount = 1,
                MeanConfidence = 0.95F,
                ErrorMessage = null
            });

        // Act
        var result = await _sut.ExtractTextAsync(pdfStream, enableOcrFallback: true, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.OcrTriggered.Should().BeTrue("OCR should be triggered for low-quality extraction");
        _mockOcrService.Verify(ocr => ocr.ExtractTextFromPdfAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractTextAsync_OcrDisabled_DoesNotTriggerOcr()
    {
        // Arrange: Low quality PDF
        var pdfStream = CreateMinimalTextPdfStream();

        // Act
        var result = await _sut.ExtractTextAsync(pdfStream, enableOcrFallback: false, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.OcrTriggered.Should().BeFalse();
        _mockOcrService.Verify(ocr => ocr.ExtractTextFromPdfAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtractTextAsync_OcrFails_FallsBackToStandardExtraction()
    {
        // Arrange: Low quality PDF
        var pdfStream = CreateMinimalTextPdfStream();

        // Setup OCR to fail
        _mockOcrService
            .Setup(ocr => ocr.ExtractTextFromPdfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrResult
            {
                Success = false,
                ErrorMessage = "OCR engine failed",
                ExtractedText = string.Empty,
                PageCount = 0
            });

        // Act
        var result = await _sut.ExtractTextAsync(pdfStream, enableOcrFallback: true, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue("Should fallback to standard extraction");
        result.OcrTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractTextAsync_CorruptPdf_ReturnsFailure()
    {
        // Arrange: Corrupt PDF (just random bytes)
        var corruptStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await _sut.ExtractTextAsync(corruptStream, enableOcrFallback: false, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }
    [Fact]
    public async Task ExtractPagedTextAsync_ValidPdf_ReturnsPageChunks()
    {
        // Arrange
        var pdfStream = CreateSimpleTestPdfStream();

        // Act
        var result = await _sut.ExtractPagedTextAsync(pdfStream, enableOcrFallback: false, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.PageChunks.Should().NotBeEmpty();
        result.PageChunks.Should().AllSatisfy(chunk => chunk.PageNumber.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task ExtractPagedTextAsync_MultiPagePdf_ReturnsCorrectPageCount()
    {
        // Arrange: Create multi-page PDF
        var pdfStream = CreateMultiPageTestPdfStream(3);

        // Act
        var result = await _sut.ExtractPagedTextAsync(pdfStream, enableOcrFallback: false, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalPages.Should().Be(3);
        result.PageChunks.Count.Should().Be(3);
    }

    [Fact]
    public async Task ExtractPagedTextAsync_EmptyStream_ReturnsFailure()
    {
        // Arrange
        var emptyStream = new MemoryStream();

        // Act
        var result = await _sut.ExtractPagedTextAsync(emptyStream, enableOcrFallback: false, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractPagedTextAsync_CorruptPdf_ReturnsFailure()
    {
        // Arrange
        var corruptStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        var result = await _sut.ExtractPagedTextAsync(corruptStream, enableOcrFallback: false, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }
    [Fact]
    public async Task ExtractTextAsync_ConcurrentCalls_HandlesSemaphoreCorrectly()
    {
        // Arrange: Create 10 concurrent extraction tasks
        var pdfStreams = Enumerable.Range(0, 10)
            .Select(_ => CreateSimpleTestPdfStream())
            .ToList();

        // Act: Execute all extractions in parallel
        var tasks = pdfStreams.Select(stream =>
            _sut.ExtractTextAsync(stream, enableOcrFallback: false, TestCancellationToken));

        var results = await Task.WhenAll(tasks);

        // Assert: All should succeed despite semaphore limiting concurrency
        results.Should().AllSatisfy(result => result.Success.Should().BeTrue());
        results.Length.Should().Be(10);
    }
    /// <summary>
    /// Creates a simple test PDF with substantial text content
    /// </summary>
    private Stream CreateSimpleTestPdfStream()
    {
        // Create a minimal valid PDF with text
        var pdfContent = @"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj
2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj
3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Contents 4 0 R
/Resources <<
/Font <<
/F1 <<
/Type /Font
/Subtype /Type1
/BaseFont /Helvetica
>>
>>
>>
>>
endobj
4 0 obj
<<
/Length 100
>>
stream
BT
/F1 12 Tf
50 700 Td
(This is a test PDF document with sufficient text content for quality assessment.) Tj
ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000314 00000 n
trailer
<<
/Size 5
/Root 1 0 R
>>
startxref
465
%%EOF";

        return new MemoryStream(System.Text.Encoding.ASCII.GetBytes(pdfContent));
    }

    /// <summary>
    /// Creates a test PDF with minimal text content (triggers OCR threshold)
    /// </summary>
    private Stream CreateMinimalTextPdfStream()
    {
        var pdfContent = @"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj
2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj
3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 612 792]
/Contents 4 0 R
/Resources <<
/Font <<
/F1 <<
/Type /Font
/Subtype /Type1
/BaseFont /Helvetica
>>
>>
>>
>>
endobj
4 0 obj
<<
/Length 30
>>
stream
BT
/F1 12 Tf
50 700 Td
(Low) Tj
ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000314 00000 n
trailer
<<
/Size 5
/Root 1 0 R
>>
startxref
395
%%EOF";

        return new MemoryStream(System.Text.Encoding.ASCII.GetBytes(pdfContent));
    }

    /// <summary>
    /// Creates a multi-page test PDF
    /// </summary>
    private Stream CreateMultiPageTestPdfStream(int pageCount)
    {
        // Generate a simple multi-page PDF structure
        var pages = new System.Text.StringBuilder();
        var pageObjects = new System.Text.StringBuilder();
        int objectId = 4; // Start after catalog, pages root, and first page

        for (int i = 0; i < pageCount; i++)
        {
            int pageObjectId = objectId++;
            int contentObjectId = objectId++;

            // Page object
            pageObjects.AppendLine($"{pageObjectId} 0 obj");
            pageObjects.AppendLine("<<");
            pageObjects.AppendLine("/Type /Page");
            pageObjects.AppendLine("/Parent 2 0 R");
            pageObjects.AppendLine("/Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >>");
            pageObjects.AppendLine($"/Contents {contentObjectId} 0 R");
            pageObjects.AppendLine(">>");
            pageObjects.AppendLine("endobj");

            // Content stream
            var content = $"BT /F1 12 Tf 50 700 Td (Page {i + 1} content) Tj ET";
            pageObjects.AppendLine($"{contentObjectId} 0 obj");
            pageObjects.AppendLine($"<< /Length {content.Length} >>");
            pageObjects.AppendLine("stream");
            pageObjects.AppendLine(content);
            pageObjects.AppendLine("endstream");
            pageObjects.AppendLine("endobj");

            // Collect page references
            if (i > 0) pages.Append(" ");
            pages.Append($"{pageObjectId} 0 R");
        }

        var pdfContent = $@"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj

2 0 obj
<<
/Type /Pages
/Kids [ {pages} ]
/Count {pageCount}
>>
endobj

{pageObjects}

xref
0 {objectId}
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
trailer
<<
/Size {objectId}
/Root 1 0 R
>>
startxref
100
%%EOF";

        return new MemoryStream(System.Text.Encoding.ASCII.GetBytes(pdfContent));
    }
}