using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Tests for ChartGenerationService
/// ISSUE-917: Chart generation validation
/// ISSUE-2601: Marked as Integration (require ScottPlot)
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class ChartGenerationServiceTests
{
    private readonly ChartGenerationService _sut;

    public ChartGenerationServiceTests()
    {
        _sut = new ChartGenerationService();
    }

    [Fact]
    public void GenerateLineChart_WithValidData_ReturnsPngBytes()
    {
        // Arrange
        var labels = new[] { "Jan", "Feb", "Mar", "Apr" };
        var values = new[] { 10.0, 20.0, 15.0, 25.0 };

        // Act
        var result = _sut.GenerateLineChart(
            "Test Line Chart",
            labels,
            values,
            "Count");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        (result.Length > 100).Should().BeTrue("PNG should be larger than 100 bytes");
        // Verify PNG signature (first 8 bytes)
        result[0].Should().Be(0x89); // PNG signature starts with 0x89
        result[1].Should().Be(0x50); // 'P'
        result[2].Should().Be(0x4E); // 'N'
        result[3].Should().Be(0x47); // 'G'
    }

    [Fact]
    public void GenerateBarChart_WithValidData_ReturnsPngBytes()
    {
        // Arrange
        var categories = new[] { "Category A", "Category B", "Category C" };
        var values = new[] { 100.0, 200.0, 150.0 };

        // Act
        var result = _sut.GenerateBarChart(
            "Test Bar Chart",
            categories,
            values,
            "Value");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        (result.Length > 100).Should().BeTrue();
        // Verify PNG signature
        result[0].Should().Be(0x89);
        result[1].Should().Be(0x50);
    }

    [Fact]
    public void GenerateMultiLineChart_WithMultipleSeries_ReturnsPngBytes()
    {
        // Arrange
        var labels = new[] { "Q1", "Q2", "Q3", "Q4" };
        var series = new Dictionary<string, double[]>
        {
            ["Revenue"] = [100.0, 150.0, 180.0, 200.0],
            ["Costs"] = [80.0, 90.0, 100.0, 110.0]
        };

        // Act
        var result = _sut.GenerateMultiLineChart(
            "Multi-Line Chart",
            labels,
            series,
            "Amount (USD)");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        (result.Length > 100).Should().BeTrue();
        result[0].Should().Be(0x89); // PNG signature
    }

    [Fact]
    public void GenerateStackedBarChart_WithMultipleSeries_ReturnsPngBytes()
    {
        // Arrange
        var categories = new[] { "Product A", "Product B" };
        var series = new Dictionary<string, double[]>
        {
            ["Q1"] = [50.0, 60.0],
            ["Q2"] = [70.0, 80.0]
        };

        // Act
        var result = _sut.GenerateStackedBarChart(
            "Stacked Bar Chart",
            categories,
            series,
            "Revenue (USD)");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        (result.Length > 100).Should().BeTrue();
        result[0].Should().Be(0x89); // PNG signature
    }

    [Fact]
    public void GenerateLineChart_WithEmptyData_ReturnsPngBytes()
    {
        // Arrange
        var labels = Array.Empty<string>();
        var values = Array.Empty<double>();

        // Act
        var result = _sut.GenerateLineChart(
            "Empty Chart",
            labels,
            values,
            "Count");

        // Assert - Should still generate a valid PNG
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result[0].Should().Be(0x89);
    }

    [Fact]
    public void GenerateBarChart_WithCustomDimensions_ReturnsLargerImage()
    {
        // Arrange
        var categories = new[] { "A", "B" };
        var values = new[] { 10.0, 20.0 };

        // Act
        var smallChart = _sut.GenerateBarChart("Small", categories, values, "Count", 400, 200);
        var largeChart = _sut.GenerateBarChart("Large", categories, values, "Count", 1200, 600);

        // Assert
        (largeChart.Length > smallChart.Length).Should().BeTrue("Larger dimensions should produce larger file");
    }

    [Fact]
    public void GenerateMultiLineChart_WithSingleSeries_ReturnsPngBytes()
    {
        // Arrange
        var labels = new[] { "Mon", "Tue", "Wed" };
        var series = new Dictionary<string, double[]>
        {
            ["Sales"] = [100.0, 120.0, 110.0]
        };

        // Act
        var result = _sut.GenerateMultiLineChart(
            "Single Series",
            labels,
            series,
            "Amount");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }
}
