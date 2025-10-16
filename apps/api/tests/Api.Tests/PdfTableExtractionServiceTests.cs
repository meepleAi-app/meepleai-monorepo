using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

using QuestPdfDocument = QuestPDF.Fluent.Document;

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
        // Generates a QuestPDF document that includes both a table and an image placeholder so the
        // structured extraction tests exercise table parsing and diagram detection without relying on iText.
        QuestPdfDocument.Create(container =>
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
        Assert.True(result.AtomicRules.Count >= table.RowCount);
        Assert.Contains(result.AtomicRules, rule => rule.Contains("Setup", StringComparison.OrdinalIgnoreCase));

        Assert.NotEmpty(result.Diagrams);
        Assert.Contains(result.Diagrams, diagram => diagram.Width > 0 && diagram.Height > 0);
    }

    [Fact]
    public void DetectTablesInPage_WithIrregularColumnsSeparatedByBlankLine_ReturnsSeparateTables()
    {
        // Arrange
        var pageText = """
Header 1   Header 2   Header 3
Value 1    Value 2    Value 3
Value 4    Value 5

Next Header 1   Next Header 2
Row A1          Row A2
""";

        // Act
        var tables = InvokeDetectTablesInPage(pageText, pageNum: 1);

        // Assert
        Assert.Equal(2, tables.Count);

        var firstTable = tables[0];
        Assert.Equal(3, firstTable.ColumnCount);
        Assert.Equal(new[] { "Header 1", "Header 2", "Header 3" }, firstTable.Headers);
        Assert.Equal(2, firstTable.RowCount);
        Assert.DoesNotContain(firstTable.Rows, row => row.All(string.IsNullOrWhiteSpace));
        Assert.Equal(string.Empty, firstTable.Rows[1][2]);

        var secondTable = tables[1];
        Assert.Equal(2, secondTable.ColumnCount);
        Assert.Equal(new[] { "Next Header 1", "Next Header 2" }, secondTable.Headers);
        Assert.Equal(1, secondTable.RowCount);
        Assert.Equal("Row A1", secondTable.Rows[0][0]);
        Assert.Equal("Row A2", secondTable.Rows[0][1]);
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
        var serviceType = typeof(PdfTableExtractionService);
        var method = serviceType.GetMethod(
            "DetectTablesInPage",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var lineType = serviceType.GetNestedType("PositionedTextLine", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PositionedTextLine type not found");
        var characterType = serviceType.GetNestedType("PositionedCharacter", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PositionedCharacter type not found");

        var addCharacterMethod = lineType.GetMethod("AddCharacter", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("AddCharacter method not found");
        var sortCharactersMethod = lineType.GetMethod("SortCharacters", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("SortCharacters method not found");

        var lineListType = typeof(List<>).MakeGenericType(lineType);
        var lines = (IList)Activator.CreateInstance(lineListType)!;

        var normalizedText = pageText.Replace("\r\n", "\n");
        var rows = normalizedText.Split('\n');
        var baseY = rows.Length * 10f;
        const float columnSpacing = 120f;
        const float characterWidth = 10f;

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            var lineY = baseY - rowIndex * 10f;
            var line = Activator.CreateInstance(lineType, new object[] { lineY })
                ?? throw new InvalidOperationException("Failed to create PositionedTextLine instance");

            if (!string.IsNullOrWhiteSpace(row))
            {
                var columns = Regex.Split(row.TrimEnd(), "\\s{2,}");

                for (var columnIndex = 0; columnIndex < columns.Length; columnIndex++)
                {
                    var columnText = columns[columnIndex];

                    if (string.IsNullOrEmpty(columnText))
                    {
                        continue;
                    }

                    var startX = columnIndex * columnSpacing;

                    for (var charIndex = 0; charIndex < columnText.Length; charIndex++)
                    {
                        var characterText = columnText[charIndex].ToString();
                        var characterX = startX + charIndex * characterWidth;
                        var character = Activator.CreateInstance(
                            characterType,
                            new object[] { characterText, characterX, lineY, characterWidth })
                            ?? throw new InvalidOperationException("Failed to create PositionedCharacter instance");

                        addCharacterMethod.Invoke(line, new[] { character });
                    }
                }

                sortCharactersMethod.Invoke(line, null);
            }

            lines.Add(line);
        }

        return (List<PdfTable>)method!.Invoke(_service, new object[] { lines, pageNum })!;
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

    [Fact]
    public void ConvertTableToAtomicRules_WithComplexTable_CreatesWellFormattedRules()
    {
        // Arrange - Complex table with multiple columns
        var table = new PdfTable
        {
            PageNumber = 5,
            Headers = new List<string> { "Action", "Cost", "Effect", "Timing" },
            Rows = new List<string[]>
            {
                new[] { "Move", "1 Action", "Move up to 3 spaces", "Any time" },
                new[] { "Attack", "1 Action", "Deal 2 damage", "Combat phase" },
                new[] { "Rest", "Free", "Gain 1 health", "End of turn" }
            },
            ColumnCount = 4
        };

        // Act
        var rules = InvokeConvertTableToAtomicRules(table);

        // Assert
        Assert.Equal(3, rules.Count);
        Assert.All(rules, rule =>
        {
            Assert.Contains("[Table on page 5]", rule);
            Assert.Contains(":", rule);
        });

        // First rule should contain all non-empty columns
        Assert.Contains("Action: Move", rules[0]);
        Assert.Contains("Cost: 1 Action", rules[0]);
        Assert.Contains("Effect: Move up to 3 spaces", rules[0]);
        Assert.Contains("Timing: Any time", rules[0]);

        // Rules should be properly delimited
        Assert.All(rules, rule => Assert.Contains("; ", rule));
    }

    [Fact]
    public void ConvertTableToAtomicRules_WithEmptyTable_ReturnsEmptyList()
    {
        // Arrange - Empty table
        var table = new PdfTable
        {
            PageNumber = 1,
            Headers = new List<string>(),
            Rows = new List<string[]>(),
            ColumnCount = 0
        };

        // Act
        var rules = InvokeConvertTableToAtomicRules(table);

        // Assert
        Assert.Empty(rules);
    }

    [Fact]
    public void ConvertTableToAtomicRules_WithTableWithoutRows_ReturnsEmptyList()
    {
        // Arrange - Table with headers but no data
        var table = new PdfTable
        {
            PageNumber = 1,
            Headers = new List<string> { "Column1", "Column2" },
            Rows = new List<string[]>(),
            ColumnCount = 2
        };

        // Act
        var rules = InvokeConvertTableToAtomicRules(table);

        // Assert
        Assert.Empty(rules);
    }

    [Fact]
    public void ConvertTableToAtomicRules_WithMultiColumnTable_PreservesAllData()
    {
        // Arrange - Wide table with many columns
        var table = new PdfTable
        {
            PageNumber = 7,
            Headers = new List<string> { "A", "B", "C", "D", "E", "F" },
            Rows = new List<string[]>
            {
                new[] { "1", "2", "3", "4", "5", "6" }
            },
            ColumnCount = 6
        };

        // Act
        var rules = InvokeConvertTableToAtomicRules(table);

        // Assert
        Assert.Single(rules);
        var rule = rules[0];

        // All headers should be present
        for (char c = 'A'; c <= 'F'; c++)
        {
            Assert.Contains($"{c}:", rule);
        }

        // All values should be present
        for (int i = 1; i <= 6; i++)
        {
            Assert.Contains(i.ToString(), rule);
        }
    }

}

