using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Linq;
using Xunit;

namespace Api.Tests;

public class PdfTableExtractionServiceTests : IDisposable
{
    private readonly Mock<ILogger<PdfTableExtractionService>> _mockLogger;
    private readonly PdfTableExtractionService _service;
    private readonly List<string> _tempFiles = new();

    public PdfTableExtractionServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        _mockLogger = new Mock<ILogger<PdfTableExtractionService>>();
        _service = new PdfTableExtractionService(_mockLogger.Object);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
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
                // Ignore cleanup failures in tests
            }
        }
    }

    private string CreateTempPdfPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"structured_{Guid.NewGuid():N}.pdf");
        _tempFiles.Add(path);
        return path;
    }

    private void CreateStructuredPdf(string filePath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text(text =>
                    {
                        text.Span("Structured Table Fixture").SemiBold().FontSize(18);
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Phase");
                            header.Cell().Element(CellStyle).Text("Task");
                            header.Cell().Element(CellStyle).Text("Count");
                        });

                        table.Cell().Element(CellStyle).Text("Setup");
                        table.Cell().Element(CellStyle).Text("Place tokens");
                        table.Cell().Element(CellStyle).Text("16");

                        table.Cell().Element(CellStyle).Text("Round Start");
                        table.Cell().Element(CellStyle).Text(string.Empty); // intentionally empty cell
                        table.Cell().Element(CellStyle).Text("8");
                    });

                    column.Item().PaddingTop(20).Image(Placeholders.Image(120, 80));
                });
            });
        }).GeneratePdf(filePath);
    }

    private static IContainer CellStyle(IContainer container) =>
        container.Border(0.5f).Padding(5).AlignMiddle();

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
    public void PdfStructuredExtractionResult_CreateSuccess_SetsPropertiesCorrectly()
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
    public void PdfStructuredExtractionResult_CreateFailure_SetsErrorMessage()
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

    [Fact]
    public async Task ExtractStructuredContentAsync_WithStructuredPdfFixture_ParsesTablesDiagramsAndAtomicRules()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateStructuredPdf(pdfPath);

        // Act
        var result = await _service.ExtractStructuredContentAsync(pdfPath);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Tables);

        var table = result.Tables.First();
        Assert.Equal(new[] { "Phase", "Task", "Count" }, table.Headers);
        Assert.Equal(table.ColumnCount, table.Headers.Count);
        Assert.True(table.RowCount >= 1);
        Assert.All(table.Rows, row => Assert.Equal(table.ColumnCount, row.Length));
        Assert.Contains(table.Rows, row =>
            row.Length == table.ColumnCount &&
            row.Any(value => string.IsNullOrWhiteSpace(value)) &&
            row.Any(value => !string.IsNullOrWhiteSpace(value)));

        Assert.NotEmpty(result.AtomicRules);
        Assert.Contains(result.AtomicRules, rule => rule.Contains("Setup", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.AtomicRules, rule => rule.Contains("Round Start", StringComparison.OrdinalIgnoreCase));

        Assert.NotEmpty(result.Diagrams);
    }
}
