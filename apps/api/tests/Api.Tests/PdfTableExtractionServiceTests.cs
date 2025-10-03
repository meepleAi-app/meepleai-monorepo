using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class PdfTableExtractionServiceTests
{
    private readonly Mock<ILogger<PdfTableExtractionService>> _mockLogger;
    private readonly PdfTableExtractionService _service;

    public PdfTableExtractionServiceTests()
    {
        _mockLogger = new Mock<ILogger<PdfTableExtractionService>>();
        _service = new PdfTableExtractionService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExtractStructuredContentAsync_WithNullPath_ReturnsFailure()
    {
        // Act
        var result = await _service.ExtractStructuredContentAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("path is required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractStructuredContentAsync_WithEmptyPath_ReturnsFailure()
    {
        // Act
        var result = await _service.ExtractStructuredContentAsync("");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractStructuredContentAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        var filePath = "nonexistent.pdf";

        // Act
        var result = await _service.ExtractStructuredContentAsync(filePath);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PdfStructuredExtractionResult_CreateSuccess_SetsPropertiesCorrectly()
    {
        // Arrange
        var tables = new List<PdfTable>
        {
            new PdfTable
            {
                PageNumber = 1,
                Headers = new List<string> { "Column1", "Column2" },
                Rows = new List<string[]> { new[] { "Value1", "Value2" } },
                ColumnCount = 2,
                RowCount = 1
            }
        };
        var diagrams = new List<PdfDiagram>
        {
            new PdfDiagram
            {
                PageNumber = 1,
                DiagramType = "Image",
                Description = "Test diagram",
                Width = 100,
                Height = 100
            }
        };
        var atomicRules = new List<string> { "Rule 1", "Rule 2" };

        // Act
        var result = PdfStructuredExtractionResult.CreateSuccess(tables, diagrams, atomicRules);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Single(result.Tables);
        Assert.Single(result.Diagrams);
        Assert.Equal(2, result.AtomicRules.Count);
        Assert.Equal(1, result.TableCount);
        Assert.Equal(1, result.DiagramCount);
        Assert.Equal(2, result.AtomicRuleCount);
    }

    [Fact]
    public async Task PdfStructuredExtractionResult_CreateFailure_SetsErrorMessage()
    {
        // Arrange
        var errorMessage = "Test error message";

        // Act
        var result = PdfStructuredExtractionResult.CreateFailure(errorMessage);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Empty(result.Tables);
        Assert.Empty(result.Diagrams);
        Assert.Empty(result.AtomicRules);
    }

    [Fact]
    public void PdfTable_Initialization_SetsDefaultValues()
    {
        // Act
        var table = new PdfTable();

        // Assert
        Assert.NotNull(table.Headers);
        Assert.NotNull(table.Rows);
        Assert.Empty(table.Headers);
        Assert.Empty(table.Rows);
        Assert.Equal(0, table.ColumnCount);
        Assert.Equal(0, table.RowCount);
    }

    [Fact]
    public void PdfDiagram_Initialization_SetsDefaultValues()
    {
        // Act
        var diagram = new PdfDiagram();

        // Assert
        Assert.Equal("Unknown", diagram.DiagramType);
        Assert.Equal(string.Empty, diagram.Description);
        Assert.Equal(0, diagram.Width);
        Assert.Equal(0, diagram.Height);
        Assert.Null(diagram.ImageData);
    }
}
