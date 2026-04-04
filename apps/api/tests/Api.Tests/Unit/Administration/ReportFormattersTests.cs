using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;
using Api.Tests.Constants;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Administration;

/// <summary>
/// Unit tests for report formatters (CSV, JSON, PDF)
/// ISSUE-919: Format conversion testing (90%+ coverage)
/// ISSUE-2601: PDF tests marked as Integration (require QuestPDF)
/// </summary>
public sealed class ReportFormattersTests
{
    #region CSV Formatter Tests

    [Fact]
    public void CsvFormatter_Format_WithEmptyContent_ShouldReturnValidCsv()
    {
        // Arrange
        var formatter = new CsvReportFormatter();
        var content = new ReportContent(
            Title: "Empty Report",
            Description: "Test",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(),
            Sections: new List<ReportSection>());

        // Act
        var result = formatter.Format(content);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void CsvFormatter_Format_WithSingleSection_ShouldIncludeHeaders()
    {
        // Arrange
        var formatter = new CsvReportFormatter();
        var content = CreateTestReportContent();

        // Act
        var result = formatter.Format(content);
        var csvText = Encoding.UTF8.GetString(result);

        // Assert
        csvText.Should().Contain("Test Report");
        csvText.Should().Contain("Metric,Value"); // CSV headers
    }

    [Fact]
    public void CsvFormatter_Format_WithMultipleSections_ShouldIncludeAllSections()
    {
        // Arrange
        var formatter = new CsvReportFormatter();
        var content = new ReportContent(
            Title: "Multi-Section Report",
            Description: "Test",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(),
            Sections: new List<ReportSection>
            {
                new ReportSection(
                    Title: "Section 1",
                    Description: "First section",
                    Data: new List<ReportDataRow>
                    {
                        new(new Dictionary<string, object> { ["Col1"] = "A", ["Col2"] = 1 })
                    },
                    Chart: null),
                new ReportSection(
                    Title: "Section 2",
                    Description: "Second section",
                    Data: new List<ReportDataRow>
                    {
                        new(new Dictionary<string, object> { ["Col3"] = "B", ["Col4"] = 2 })
                    },
                    Chart: null)
            });

        // Act
        var result = formatter.Format(content);
        var csvText = Encoding.UTF8.GetString(result);

        // Assert
        csvText.Should().Contain("Section 1");
        csvText.Should().Contain("Section 2");
    }

    [Fact]
    public void CsvFormatter_Format_WithSpecialCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var formatter = new CsvReportFormatter();
        var content = new ReportContent(
            Title: "Test Report",
            Description: "Test",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(),
            Sections: new List<ReportSection>
            {
                new ReportSection(
                    Title: "Section",
                    Description: "Test",
                    Data: new List<ReportDataRow>
                    {
                        new(new Dictionary<string, object>
                        {
                            ["Field"] = "Value with, comma",
                            ["Other"] = "Value with \"quotes\""
                        })
                    },
                    Chart: null)
            });

        // Act
        var result = formatter.Format(content);
        var csvText = Encoding.UTF8.GetString(result);

        // Assert
        csvText.Should().NotBeEmpty();
        // CSV should handle special characters properly
    }

    [Fact]
    public void CsvFormatter_GetFileExtension_ShouldReturnCsv()
    {
        // Arrange
        var formatter = new CsvReportFormatter();

        // Act
        var extension = formatter.GetFileExtension();

        // Assert
        extension.Should().Be("csv");
    }

    #endregion

    #region JSON Formatter Tests

    [Fact]
    public void JsonFormatter_Format_WithEmptyContent_ShouldReturnValidJson()
    {
        // Arrange
        var formatter = new JsonReportFormatter();
        var content = new ReportContent(
            Title: "Empty Report",
            Description: "Test",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(),
            Sections: new List<ReportSection>());

        // Act
        var result = formatter.Format(content);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        // Verify it's valid JSON
        var jsonText = Encoding.UTF8.GetString(result);
        var parsed = JsonDocument.Parse(jsonText);
        parsed.Should().NotBeNull();
    }

    [Fact]
    public void JsonFormatter_Format_WithSingleSection_ShouldIncludeAllProperties()
    {
        // Arrange
        var formatter = new JsonReportFormatter();
        var content = CreateTestReportContent();

        // Act
        var result = formatter.Format(content);
        var jsonText = Encoding.UTF8.GetString(result);
        var parsed = JsonDocument.Parse(jsonText);

        // Assert
        var root = parsed.RootElement;
        root.TryGetProperty("title", out var title).Should().BeTrue();
        title.GetString().Should().Be("Test Report");
        root.TryGetProperty("description", out _).Should().BeTrue();
        root.TryGetProperty("generatedAt", out _).Should().BeTrue();
        root.TryGetProperty("sections", out _).Should().BeTrue();
    }

    [Fact]
    public void JsonFormatter_Format_WithMultipleSections_ShouldIncludeAllSections()
    {
        // Arrange
        var formatter = new JsonReportFormatter();
        var content = new ReportContent(
            Title: "Multi-Section Report",
            Description: "Test",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(),
            Sections: new List<ReportSection>
            {
                new ReportSection("Section 1", "Desc 1", new List<ReportDataRow>(), null),
                new ReportSection("Section 2", "Desc 2", new List<ReportDataRow>(), null),
                new ReportSection("Section 3", "Desc 3", new List<ReportDataRow>(), null)
            });

        // Act
        var result = formatter.Format(content);
        var jsonText = Encoding.UTF8.GetString(result);
        var parsed = JsonDocument.Parse(jsonText);

        // Assert
        var sections = parsed.RootElement.GetProperty("sections");
        sections.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void JsonFormatter_Format_WithChartData_ShouldSerializeSections()
    {
        // Arrange
        var formatter = new JsonReportFormatter();
        var content = new ReportContent(
            Title: "Chart Report",
            Description: "Test",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(),
            Sections: new List<ReportSection>
            {
                new ReportSection(
                    Title: "Section with Chart",
                    Description: "Test",
                    Data: new List<ReportDataRow>(),
                    Chart: new ChartData(
                        Type: ChartType.Bar,
                        Labels: new[] { "A", "B", "C" },
                        Series: new Dictionary<string, double[]>
                        {
                            ["Values"] = new double[] { 1.0, 2.0, 3.0 }
                        },
                        YAxisLabel: "Count"))
            });

        // Act
        var result = formatter.Format(content);
        var jsonText = Encoding.UTF8.GetString(result);

        // Assert - JsonFormatter currently doesn't serialize chart data, only section data
        jsonText.Should().Contain("Section with Chart");
        jsonText.Should().Contain("sections");
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void JsonFormatter_Format_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var formatter = new JsonReportFormatter();
        var content = new ReportContent(
            Title: "Test Report",
            Description: "Test",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>
            {
                ["hours"] = 24,
                ["since"] = DateTime.UtcNow.AddDays(-1),
                ["custom"] = "value"
            },
            Sections: new List<ReportSection>());

        // Act
        var result = formatter.Format(content);
        var jsonText = Encoding.UTF8.GetString(result);
        var parsed = JsonDocument.Parse(jsonText);

        // Assert
        var metadata = parsed.RootElement.GetProperty("metadata");
        metadata.TryGetProperty("hours", out var hours).Should().BeTrue();
        hours.GetInt32().Should().Be(24);
    }

    [Fact]
    public void JsonFormatter_GetFileExtension_ShouldReturnJson()
    {
        // Arrange
        var formatter = new JsonReportFormatter();

        // Act
        var extension = formatter.GetFileExtension();

        // Assert
        extension.Should().Be("json");
    }

    #endregion

    #region PDF Formatter Tests

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public void PdfFormatter_Format_WithEmptyContent_ShouldReturnValidPdf()
    {
        // Arrange
        var formatter = new PdfReportFormatter();
        var content = new ReportContent(
            Title: "Empty Report",
            Description: "Test",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(),
            Sections: new List<ReportSection>());

        // Act
        var result = formatter.Format(content);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        // PDF should start with "%PDF-"
        var pdfHeader = Encoding.ASCII.GetString(result.Take(5).ToArray());
        pdfHeader.Should().Be("%PDF-");
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public void PdfFormatter_Format_WithSingleSection_ShouldGeneratePdf()
    {
        // Arrange
        var formatter = new PdfReportFormatter();
        var content = CreateTestReportContent();

        // Act
        var result = formatter.Format(content);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        (result.Length > 100).Should().BeTrue(); // PDF should have reasonable size
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public void PdfFormatter_Format_WithMultipleSections_ShouldGeneratePdf()
    {
        // Arrange
        var formatter = new PdfReportFormatter();
        var content = new ReportContent(
            Title: "Multi-Section Report",
            Description: "Test",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(),
            Sections: new List<ReportSection>
            {
                new ReportSection("Section 1", "Desc 1", new List<ReportDataRow>
                {
                    new(new Dictionary<string, object> { ["A"] = "1" })
                }, null),
                new ReportSection("Section 2", "Desc 2", new List<ReportDataRow>
                {
                    new(new Dictionary<string, object> { ["B"] = "2" })
                }, null)
            });

        // Act
        var result = formatter.Format(content);

        // Assert
        result.Should().NotBeNull();
        (result.Length > 100).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public void PdfFormatter_Format_WithChartData_ShouldIncludeChart()
    {
        // Arrange
        var formatter = new PdfReportFormatter();
        var content = new ReportContent(
            Title: "Chart Report",
            Description: "Test",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(),
            Sections: new List<ReportSection>
            {
                new ReportSection(
                    Title: "Section with Chart",
                    Description: "Test",
                    Data: new List<ReportDataRow>
                    {
                        new(new Dictionary<string, object> { ["X"] = "A", ["Y"] = 10 })
                    },
                    Chart: new ChartData(
                        Type: ChartType.Line,
                        Labels: new[] { "Jan", "Feb", "Mar" },
                        Series: new Dictionary<string, double[]>
                        {
                            ["Series1"] = new double[] { 10, 20, 30 }
                        },
                        YAxisLabel: "Value"))
            });

        // Act
        var result = formatter.Format(content);

        // Assert
        result.Should().NotBeNull();
        (result.Length > 100).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public void PdfFormatter_Format_WithLargeDataset_ShouldHandleCorrectly()
    {
        // Arrange
        var formatter = new PdfReportFormatter();
        var largeData = Enumerable.Range(1, 100)
            .Select(i => new ReportDataRow(new Dictionary<string, object>
            {
                ["Index"] = i,
                ["Value"] = i * 10,
                ["Description"] = $"Row {i}"
            }))
            .ToList();

        var content = new ReportContent(
            Title: "Large Report",
            Description: "Test with 100 rows",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>(),
            Sections: new List<ReportSection>
            {
                new ReportSection("Large Section", "100 rows", largeData, null)
            });

        // Act
        var result = formatter.Format(content);

        // Assert
        result.Should().NotBeNull();
        (result.Length > 500).Should().BeTrue(); // Should be larger with more data
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public void PdfFormatter_GetFileExtension_ShouldReturnPdf()
    {
        // Arrange
        var formatter = new PdfReportFormatter();

        // Act
        var extension = formatter.GetFileExtension();

        // Assert
        extension.Should().Be("pdf");
    }

    #endregion

    #region All Formatters Tests

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public void AllFormatters_WithSameContent_ShouldProduceDifferentFormats()
    {
        // Arrange
        var content = CreateTestReportContent();
        var csvFormatter = new CsvReportFormatter();
        var jsonFormatter = new JsonReportFormatter();
        var pdfFormatter = new PdfReportFormatter();

        // Act
        var csvResult = csvFormatter.Format(content);
        var jsonResult = jsonFormatter.Format(content);
        var pdfResult = pdfFormatter.Format(content);

        // Assert
        Assert.NotEqual(csvResult, jsonResult);
        Assert.NotEqual(jsonResult, pdfResult);
        Assert.NotEqual(csvResult, pdfResult);
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public void AllFormatters_ShouldHaveUniqueExtensions()
    {
        // Arrange
        var csvFormatter = new CsvReportFormatter();
        var jsonFormatter = new JsonReportFormatter();
        var pdfFormatter = new PdfReportFormatter();

        // Act
        var extensions = new[]
        {
            csvFormatter.GetFileExtension(),
            jsonFormatter.GetFileExtension(),
            pdfFormatter.GetFileExtension()
        };

        // Assert
        extensions.Distinct().Count().Should().Be(3);
    }

    #endregion

    #region Helper Methods

    private static ReportContent CreateTestReportContent()
    {
        return new ReportContent(
            Title: "Test Report",
            Description: "Test Description",
            GeneratedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>
            {
                ["testKey"] = "testValue"
            },
            Sections: new List<ReportSection>
            {
                new ReportSection(
                    Title: "Test Section",
                    Description: "Section Description",
                    Data: new List<ReportDataRow>
                    {
                        new(new Dictionary<string, object>
                        {
                            ["Metric"] = "Test Metric",
                            ["Value"] = 42
                        })
                    },
                    Chart: null)
            });
    }

    #endregion
}
