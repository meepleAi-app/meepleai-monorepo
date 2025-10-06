using System;
using System.IO;
using System.Linq;
using Api.Services;
using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
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
    public async Task ExtractStructuredContentAsync_WithPdfContainingTableAndImage_PopulatesStructuredCollections()
    {
        // Arrange
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");

        try
        {
            using (var writer = new PdfWriter(tempFilePath))
            using (var pdfDocument = new PdfDocument(writer))
            using (var document = new Document(pdfDocument))
            {
                var table = new Table(2);
                table.AddHeaderCell("Header 1");
                table.AddHeaderCell("Header 2");
                table.AddCell("Row 1 Column 1");
                table.AddCell("Row 1 Column 2");
                table.AddCell("Row 2 Column 1");
                table.AddCell("Row 2 Column 2");
                document.Add(table);

                var imageBytes = Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=");
                var imageData = ImageDataFactory.Create(imageBytes);
                var image = new Image(imageData).ScaleToFit(50, 50);
                document.Add(image);
            }

            // Act
            var result = await _service.ExtractStructuredContentAsync(tempFilePath);

            // Assert
            Assert.True(result.Success);
            Assert.NotEmpty(result.Tables);

            var firstTable = result.Tables.First();
            Assert.True(firstTable.ColumnCount >= 2);
            Assert.True(firstTable.RowCount >= 2);

            Assert.True(result.AtomicRules.Count >= firstTable.RowCount);
            Assert.Contains(result.AtomicRules, rule => rule.Contains("Row 1 Column 1", StringComparison.OrdinalIgnoreCase));

            Assert.NotEmpty(result.Diagrams);
            Assert.Contains(result.Diagrams, diagram => diagram.Width > 0 && diagram.Height > 0);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public void DetectTablesInPage_WithIrregularColumnsAndEndOfPage_ReturnsExpectedTables()
    {
        // Arrange
        var pageText = """
Header 1   Header 2   Header 3
Value 1    Value 2    Value 3
Value 4    Value 5

Another Header 1   Another Header 2
Row A1             Row A2
""";

        // Act
        var tables = InvokeDetectTablesInPage(pageText, pageNum: 1);

        // Assert
        Assert.Equal(2, tables.Count);

        var firstTable = tables[0];
        Assert.Equal(3, firstTable.ColumnCount);
        Assert.Equal(2, firstTable.RowCount);
        Assert.Equal(2, firstTable.Rows[1].Length); // Irregular columns handled

        var secondTable = tables[1];
        Assert.Equal(2, secondTable.ColumnCount);
        Assert.Equal(1, secondTable.RowCount);
    }

    [Fact]
    public void ConvertTableToAtomicRules_WithSparseRows_CreatesRulesForPopulatedValues()
    {
        // Arrange
        var table = new PdfTable
        {
            PageNumber = 2,
            Headers = new List<string> { "Col1", "Col2", "Col3" },
            Rows = new List<string[]>
            {
                new[] { "Value1", "Value2", string.Empty },
                new[] { "OnlyFirst", string.Empty, string.Empty },
                new[] { string.Empty, string.Empty, string.Empty }
            },
            ColumnCount = 3
        };

        // Act
        var rules = InvokeConvertTableToAtomicRules(table);

        // Assert
        Assert.Equal(2, rules.Count);
        Assert.Contains(rules, rule => rule.Contains("Col1: Value1") && rule.Contains("Col2: Value2"));
        Assert.Contains(rules, rule => rule.Contains("Col1: OnlyFirst") && !rule.Contains("Col2:"));
    }

    [Fact]
    public async Task ExtractStructuredContentAsync_WithCorruptedPdf_LogsErrorAndReturnsFailure()
    {
        // Arrange
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        await File.WriteAllTextAsync(tempFilePath, "not a valid pdf");

        try
        {
            // Act
            var result = await _service.ExtractStructuredContentAsync(tempFilePath);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("Extraction failed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Failed to extract structured content")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private List<PdfTable> InvokeDetectTablesInPage(string pageText, int pageNum)
    {
        var method = typeof(PdfTableExtractionService).GetMethod(
            "DetectTablesInPage",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (List<PdfTable>)method!.Invoke(_service, new object[] { pageText, pageNum })!;
    }

    private List<string> InvokeConvertTableToAtomicRules(PdfTable table)
    {
        var method = typeof(PdfTableExtractionService).GetMethod(
            "ConvertTableToAtomicRules",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (List<string>)method!.Invoke(_service, new object[] { table })!;
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
    public async Task ExtractStructuredContentAsync_WithStructuredPdf_ReturnsTablesAndDiagrams()
    {
        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample_table.pdf");

        // Act
        var result = await _service.ExtractStructuredContentAsync(fixturePath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.TableCount);
        Assert.Equal(result.TableCount, result.Tables.Count);
        var table = Assert.Single(result.Tables);
        Assert.Equal(new[] { "Name", "Value" }, table.Headers);
        Assert.Equal(3, table.RowCount);
        Assert.Equal(3, result.AtomicRuleCount);
        Assert.Contains(result.AtomicRules, rule => rule.Contains("Name: Alpha", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(result.Diagrams);
        Assert.Equal(result.Diagrams.Count, result.DiagramCount);
        Assert.All(result.Diagrams, diagram => Assert.Equal("Image", diagram.DiagramType));
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
