using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Api.Services;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;
using FluentAssertions;
using Xunit;

using QuestPdfDocument = QuestPDF.Fluent.Document;

namespace Api.Tests;

public class PdfTableExtractionServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<PdfTableExtractionService>> _mockLogger;
    private readonly PdfTableExtractionService _service;
    private readonly List<string> _tempFiles = new();

    public PdfTableExtractionServiceTests(ITestOutputHelper output)
    {
        _output = output;
        QuestPDF.Settings.License = LicenseType.Community;
        _mockLogger = new Mock<ILogger<PdfTableExtractionService>>();
        _service = new PdfTableExtractionService(
            Mock.Of<ITableDetectionService>(),
            Mock.Of<ITableStructureAnalyzer>(),
            _mockLogger.Object);
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
        result.Success.Should().BeTrue();
        result.Tables.Should().NotBeEmpty();

        var table = result.Tables.First();
        table.Headers.Should().BeEquivalentTo(new[] { "Phase", "Task", "Count" });
        table.Headers.Count.Should().Be(table.ColumnCount);
        (table.RowCount >= 1).Should().BeTrue();
        (result.AtomicRules.Count >= table.RowCount).Should().BeTrue();
        result.AtomicRules.Should().Contain(rule => rule.Contains("Setup"));

        result.Diagrams.Should().NotBeEmpty();
        result.Diagrams.Should().Contain(diagram => diagram.Width > 0 && diagram.Height > 0);
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
        tables.Count.Should().Be(2);

        var firstTable = tables[0];
        firstTable.ColumnCount.Should().Be(3);
        firstTable.Headers.Should().BeEquivalentTo(new[] { "Header 1", "Header 2", "Header 3" });
        firstTable.RowCount.Should().Be(2);
        firstTable.Rows.Should().NotContain(row => row.All(string.IsNullOrWhiteSpace));
        firstTable.Rows[1][2].Should().Be(string.Empty);

        var secondTable = tables[1];
        secondTable.ColumnCount.Should().Be(2);
        secondTable.Headers.Should().BeEquivalentTo(new[] { "Next Header 1", "Next Header 2" });
        secondTable.RowCount.Should().Be(1);
        secondTable.Rows[0][0].Should().Be("Row A1");
        secondTable.Rows[0][1].Should().Be("Row A2");
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
        rules.Count.Should().Be(2);
        rules.Should().Contain(rule => rule.Contains("Col1: Value1") && rule.Contains("Col2: Value2"));
        rules.Should().Contain(rule => rule.Contains("Col1: OnlyFirst") && !rule.Contains("Col2:"));
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
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNull();
            result.ErrorMessage.Should().Contain("Extraction failed");

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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("path is required");
    }

    [Fact]
    public async Task ExtractStructuredContentAsync_WithEmptyPath_ReturnsFailure()
    {
        // Act
        var result = await _service.ExtractStructuredContentAsync("");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractStructuredContentAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        var filePath = "nonexistent.pdf";

        // Act
        var result = await _service.ExtractStructuredContentAsync(filePath);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("not found");
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
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Tables.Should().ContainSingle();
        result.Diagrams.Should().ContainSingle();
        result.AtomicRules.Count.Should().Be(2);
        result.TableCount.Should().Be(1);
        result.DiagramCount.Should().Be(1);
        result.AtomicRuleCount.Should().Be(2);
    }

    [Fact]
    public void PdfStructuredExtractionResult_CreateFailure_SetsErrorMessage()
    {
        // Arrange
        var errorMessage = "Test error message";

        // Act
        var result = PdfStructuredExtractionResult.CreateFailure(errorMessage);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeEquivalentTo(errorMessage);
        result.Tables.Should().BeEmpty();
        result.Diagrams.Should().BeEmpty();
        result.AtomicRules.Should().BeEmpty();
    }

    [Fact]
    public void PdfTable_Initialization_SetsDefaultValues()
    {
        // Act
        var table = new PdfTable();

        // Assert
        table.Headers.Should().NotBeNull();
        table.Rows.Should().NotBeNull();
        table.Headers.Should().BeEmpty();
        table.Rows.Should().BeEmpty();
        table.ColumnCount.Should().Be(0);
        table.RowCount.Should().Be(0);
    }

    [Fact]
    public void PdfDiagram_Initialization_SetsDefaultValues()
    {
        // Act
        var diagram = new PdfDiagram();

        // Assert
        diagram.DiagramType.Should().Be("Unknown");
        diagram.Description.Should().Be(string.Empty);
        diagram.Width.Should().Be(0);
        diagram.Height.Should().Be(0);
        diagram.ImageData.Should().BeNull();
    }

    [Fact]
    public async Task ExtractStructuredContentAsync_WithStructuredPdf_ReturnsTablesAndDiagrams()
    {
        // Arrange
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample_table.pdf");

        // Act
        var result = await _service.ExtractStructuredContentAsync(fixturePath);

        // Assert
        result.Success.Should().BeTrue();
        result.TableCount.Should().Be(1);
        result.Tables.Count.Should().Be(result.TableCount);
        var table = result.Tables.Should().ContainSingle().Subject;
        table.Headers.Should().BeEquivalentTo(new[] { "Name", "Value" });
        table.RowCount.Should().Be(3);
        result.AtomicRuleCount.Should().Be(3);
        result.AtomicRules.Should().Contain(rule => rule.Contains("Name: Alpha"));
        result.Diagrams.Should().NotBeEmpty();
        result.DiagramCount.Should().Be(result.Diagrams.Count);
        result.Diagrams.Should().OnlyContain(diagram => diagram.DiagramType == "Image");
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
        result.Success.Should().BeTrue();
        result.Tables.Should().NotBeEmpty();

        var table = result.Tables.First();
        table.Headers.Should().BeEquivalentTo(new[] { "Phase", "Task", "Count" });
        table.Headers.Count.Should().Be(table.ColumnCount);
        (table.RowCount >= 1).Should().BeTrue();
        table.Rows.Should().OnlyContain(row => row.Length == table.ColumnCount);
        table.Rows.Should().Contain(row =>
            row.Length == table.ColumnCount &&
            row.Any(value => string.IsNullOrWhiteSpace(value)) &&
            row.Any(value => !string.IsNullOrWhiteSpace(value)));

        result.AtomicRules.Should().NotBeEmpty();
        result.AtomicRules.Should().Contain(rule => rule.Contains("Setup"));
        result.AtomicRules.Should().Contain(rule => rule.Contains("Round Start"));

        result.Diagrams.Should().NotBeEmpty();
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
        rules.Count.Should().Be(3);
        rules.Should().OnlyContain(rule =>
            rule.Contains("[Table on page 5]") &&
            rule.Contains(":"));

        // First rule should contain all non-empty columns
        rules[0].Should().Contain("Action: Move");
        rules[0].Should().Contain("Cost: 1 Action");
        rules[0].Should().Contain("Effect: Move up to 3 spaces");
        rules[0].Should().Contain("Timing: Any time");

        // Rules should be properly delimited
        rules.Should().OnlyContain(rule => rule.Contains("; "));
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
        rules.Should().BeEmpty();
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
        rules.Should().BeEmpty();
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
        rules.Should().ContainSingle();
        var rule = rules[0];

        // All headers should be present
        for (char c = 'A'; c <= 'F'; c++)
        {
            rule.Should().Contain($"{c}:");
        }

        // All values should be present
        for (int i = 1; i <= 6; i++)
        {
            rule.Should().Contain(i.ToString());
        }
    }

}

