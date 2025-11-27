using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Integration tests for ITextPdfTableExtractor adapter
/// Tests infrastructure logic with mocked dependencies
/// </summary>
public class ITextPdfTableExtractorTests
{
    private readonly Mock<ITableDetectionService> _mockTableDetection;
    private readonly Mock<ITableStructureAnalyzer> _mockTableAnalyzer;
    private readonly TableToAtomicRuleConverter _ruleConverter;
    private readonly ILogger<ITextPdfTableExtractor> _logger;
    private readonly ITextPdfTableExtractor _extractor;

    public ITextPdfTableExtractorTests()
    {
        _mockTableDetection = new Mock<ITableDetectionService>();
        _mockTableAnalyzer = new Mock<ITableStructureAnalyzer>();
        _ruleConverter = new TableToAtomicRuleConverter();
        _logger = NullLogger<ITextPdfTableExtractor>.Instance;

        _extractor = new ITextPdfTableExtractor(
            _mockTableDetection.Object,
            _mockTableAnalyzer.Object,
            _ruleConverter,
            _logger);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullTableDetectionService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ITextPdfTableExtractor(
            null!,
            _mockTableAnalyzer.Object,
            _ruleConverter,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullTableStructureAnalyzer_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ITextPdfTableExtractor(
            _mockTableDetection.Object,
            null!,
            _ruleConverter,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullRuleConverter_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ITextPdfTableExtractor(
            _mockTableDetection.Object,
            _mockTableAnalyzer.Object,
            null!,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ITextPdfTableExtractor(
            _mockTableDetection.Object,
            _mockTableAnalyzer.Object,
            _ruleConverter,
            null!));
    }

    #endregion

    #region ExtractTablesAsync - Validation Tests

    [Fact]
    public async Task ExtractTablesAsync_WithNullFilePath_ReturnsFailureResult()
    {
        // Act
        var result = await _extractor.ExtractTablesAsync(null!, true, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTablesAsync_WithEmptyFilePath_ReturnsFailureResult()
    {
        // Act
        var result = await _extractor.ExtractTablesAsync("", true, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTablesAsync_WithWhitespaceFilePath_ReturnsFailureResult()
    {
        // Act
        var result = await _extractor.ExtractTablesAsync("   ", true, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTablesAsync_WithNonExistentFile_ReturnsFailureResult()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.pdf");

        // Act
        var result = await _extractor.ExtractTablesAsync(nonExistentPath, true, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File not found", result.ErrorMessage);
    }

    #endregion

    #region ExtractStructuredContentAsync - Validation Tests

    [Fact]
    public async Task ExtractStructuredContentAsync_WithNullFilePath_ReturnsFailureResult()
    {
        // Act
        var result = await _extractor.ExtractStructuredContentAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractStructuredContentAsync_WithEmptyFilePath_ReturnsFailureResult()
    {
        // Act
        var result = await _extractor.ExtractStructuredContentAsync("", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractStructuredContentAsync_WithNonExistentFile_ReturnsFailureResult()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.pdf");

        // Act
        var result = await _extractor.ExtractStructuredContentAsync(nonExistentPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File not found", result.ErrorMessage);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ExtractTablesAsync_WithCancellationToken_PropagatesCancellation()
    {
        // Arrange
        var tempPdfPath = CreateTempPdfFile();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        try
        {
            // Act & Assert
            // TaskCanceledException is a subclass of OperationCanceledException
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await _extractor.ExtractTablesAsync(tempPdfPath, true, cts.Token));

            Assert.True(exception is OperationCanceledException or TaskCanceledException);
        }
        finally
        {
            if (File.Exists(tempPdfPath))
                File.Delete(tempPdfPath);
        }
    }

    [Fact]
    public async Task ExtractStructuredContentAsync_WithCancellationToken_PropagatesCancellation()
    {
        // Arrange
        var tempPdfPath = CreateTempPdfFile();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        try
        {
            // Act & Assert
            // TaskCanceledException is a subclass of OperationCanceledException
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await _extractor.ExtractStructuredContentAsync(tempPdfPath, cts.Token));

            Assert.True(exception is OperationCanceledException or TaskCanceledException);
        }
        finally
        {
            if (File.Exists(tempPdfPath))
                File.Delete(tempPdfPath);
        }
    }

    #endregion

    #region Result Structure Tests

    [Fact]
    public void TableExtractionResult_CreateSuccess_ReturnsSuccessResult()
    {
        // Arrange
        var tables = new List<PdfTable>
        {
            new PdfTable { PageNumber = 1, Headers = new List<string> { "Test" } }
        };
        var rules = new List<string> { "Test rule" };

        // Act
        var result = TableExtractionResult.CreateSuccess(tables, rules);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(1, result.TableCount);
        Assert.Equal(1, result.AtomicRuleCount);
        Assert.Equal(tables, result.Tables);
        Assert.Equal(rules, result.AtomicRules);
    }

    [Fact]
    public void TableExtractionResult_CreateFailure_ReturnsFailureResult()
    {
        // Arrange
        var errorMessage = "Test error";

        // Act
        var result = TableExtractionResult.CreateFailure(errorMessage);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Equal(0, result.TableCount);
        Assert.Equal(0, result.AtomicRuleCount);
    }

    [Fact]
    public void StructuredContentResult_CreateSuccess_ReturnsSuccessResult()
    {
        // Arrange
        var tables = new List<PdfTable>
        {
            new PdfTable { PageNumber = 1, Headers = new List<string> { "Test" } }
        };
        var diagrams = new List<PdfDiagram>
        {
            new PdfDiagram { PageNumber = 1, DiagramType = "flowchart" }
        };
        var rules = new List<string> { "Test rule" };

        // Act
        var result = StructuredContentResult.CreateSuccess(tables, diagrams, rules);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(1, result.TableCount);
        Assert.Equal(1, result.DiagramCount);
        Assert.Equal(1, result.AtomicRuleCount);
    }

    [Fact]
    public void StructuredContentResult_CreateFailure_ReturnsFailureResult()
    {
        // Arrange
        var errorMessage = "Test error";

        // Act
        var result = StructuredContentResult.CreateFailure(errorMessage);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Equal(0, result.TableCount);
        Assert.Equal(0, result.DiagramCount);
        Assert.Equal(0, result.AtomicRuleCount);
    }

    #endregion

    #region DTO Tests

    [Fact]
    public void PdfTable_DefaultConstructor_InitializesCollections()
    {
        // Act
        var table = new PdfTable();

        // Assert
        Assert.NotNull(table.Headers);
        Assert.NotNull(table.Rows);
        Assert.Empty(table.Headers);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void PdfTable_WithData_StoresCorrectly()
    {
        // Arrange & Act
        var table = new PdfTable
        {
            PageNumber = 5,
            StartLine = 10,
            Headers = new List<string> { "Col1", "Col2" },
            Rows = new List<string[]> { new[] { "A", "B" }, new[] { "C", "D" } },
            ColumnCount = 2,
            RowCount = 2
        };

        // Assert
        Assert.Equal(5, table.PageNumber);
        Assert.Equal(10, table.StartLine);
        Assert.Equal(2, table.Headers.Count);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.ColumnCount);
        Assert.Equal(2, table.RowCount);
    }

    [Fact]
    public void PdfDiagram_WithData_StoresCorrectly()
    {
        // Arrange & Act
        var diagram = new PdfDiagram
        {
            PageNumber = 3,
            DiagramType = "sequence",
            Width = 100,
            Height = 200,
            Description = "Test diagram",
            ImageData = new byte[] { 1, 2, 3 }
        };

        // Assert
        Assert.Equal(3, diagram.PageNumber);
        Assert.Equal("sequence", diagram.DiagramType);
        Assert.Equal(100, diagram.Width);
        Assert.Equal(200, diagram.Height);
        Assert.Equal("Test diagram", diagram.Description);
        Assert.Equal(3, diagram.ImageData!.Length);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a minimal valid PDF file for testing
    /// </summary>
    private static string CreateTempPdfFile()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.pdf");

        // Minimal valid PDF structure (PDF 1.4)
        var pdfContent = @"%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /MediaBox [0 0 612 792] /Contents 4 0 R >>
endobj
4 0 obj
<< /Length 44 >>
stream
BT /F1 12 Tf 100 700 Td (Test PDF) Tj ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000317 00000 n
trailer
<< /Size 5 /Root 1 0 R >>
startxref
410
%%EOF";

        File.WriteAllText(tempPath, pdfContent);
        return tempPath;
    }

    #endregion
}

