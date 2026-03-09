using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the ContentComplexityScore value object.
/// Issue #5451: Content complexity routing.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ContentComplexityScoreTests
{
    [Fact]
    public void ComputeFromText_WithEmptyContent_ReturnsZeroScore()
    {
        // Act
        var score = ContentComplexityScore.ComputeFromText("");

        // Assert
        score.Score.Should().Be(0m);
        score.EstimatedPageCount.Should().Be(0);
        score.DetectedTableCount.Should().Be(0);
        score.Decision.Should().Be(RoutingDecision.Synchronous);
    }

    [Fact]
    public void ComputeFromText_WithNullContent_ReturnsZeroScore()
    {
        // Act
        var score = ContentComplexityScore.ComputeFromText(null!);

        // Assert
        score.Score.Should().Be(0m);
        score.Decision.Should().Be(RoutingDecision.Synchronous);
    }

    [Fact]
    public void ComputeFromText_WithShortTextOnly_ReturnsSynchronous()
    {
        // Arrange - ~2 pages of text, no tables, no images
        var content = new string('A', 6000);

        // Act
        var score = ContentComplexityScore.ComputeFromText(content);

        // Assert
        score.Score.Should().BeLessThan(0.4m);
        score.Decision.Should().Be(RoutingDecision.Synchronous);
        score.EstimatedPageCount.Should().Be(2);
    }

    [Fact]
    public void ComputeFromText_WithLargeContent_RoutesToBackground()
    {
        // Arrange - large text with tables and image markers to push above threshold
        var textPart = new string('A', 150000); // ~50 pages → page component = 0.3
        var tableRows = string.Join("\n", Enumerable.Range(0, 30)
            .Select(i => $"| Col1-{i} | Col2-{i} | Col3-{i} |")); // 10 tables → table component
        var imageMarkers = string.Join("\n", Enumerable.Range(0, 10)
            .Select(i => $"Text before [image] text after {i}")); // image markers
        var content = textPart + "\n" + tableRows + "\n" + imageMarkers;

        // Act
        var score = ContentComplexityScore.ComputeFromText(content);

        // Assert
        score.Score.Should().BeGreaterThan(0.4m);
        score.Decision.Should().Be(RoutingDecision.Background);
    }

    [Fact]
    public void ComputeFromText_WithManyTables_IncreasesComplexity()
    {
        // Arrange - short text with many pipe tables
        var tableRows = string.Join("\n", Enumerable.Range(0, 60)
            .Select(i => $"| Col1 | Col2 | Col3 |"));
        var content = "Some intro text\n" + tableRows;

        // Act
        var score = ContentComplexityScore.ComputeFromText(content);

        // Assert
        score.DetectedTableCount.Should().BeGreaterThan(0);
        score.Score.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void ComputeFromText_WithImageMarkers_IncreasesComplexity()
    {
        // Arrange - text with many image placeholders
        var content = string.Join("\n", Enumerable.Range(0, 20)
            .Select(i => $"Some text before [image] and after image {i}"));

        // Act
        var score = ContentComplexityScore.ComputeFromText(content);

        // Assert
        score.ImageRatio.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void CreateFromMetadata_WithHighComplexity_RoutesToBackground()
    {
        // Arrange - 50 pages, 15 tables, 0.5 image ratio, OCR required
        // Act
        var score = ContentComplexityScore.CreateFromMetadata(50, 15, 0.5m, true);

        // Assert
        score.Score.Should().BeGreaterThan(0.4m);
        score.Decision.Should().Be(RoutingDecision.Background);
        score.EstimatedPageCount.Should().Be(50);
        score.DetectedTableCount.Should().Be(15);
        score.ImageRatio.Should().Be(0.5m);
        score.OcrRequired.Should().BeTrue();
    }

    [Fact]
    public void CreateFromMetadata_WithLowComplexity_RoutesSynchronous()
    {
        // Arrange - 5 pages, 1 table, 0.0 image ratio, no OCR
        // Act
        var score = ContentComplexityScore.CreateFromMetadata(5, 1, 0.0m, false);

        // Assert
        score.Score.Should().BeLessThan(0.4m);
        score.Decision.Should().Be(RoutingDecision.Synchronous);
    }

    [Fact]
    public void CreateFromMetadata_WithOcrOnly_Adds02()
    {
        // Arrange - minimal content but OCR required
        var withOcr = ContentComplexityScore.CreateFromMetadata(1, 0, 0m, true);
        var withoutOcr = ContentComplexityScore.CreateFromMetadata(1, 0, 0m, false);

        // Assert
        (withOcr.Score - withoutOcr.Score).Should().Be(0.2m);
    }

    [Fact]
    public void Score_IsClamped_Between0And1()
    {
        // Arrange - extreme values
        var score = ContentComplexityScore.CreateFromMetadata(1000, 1000, 5.0m, true);

        // Assert
        score.Score.Should().BeLessThanOrEqualTo(1.0m);
        score.Score.Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Act
        var score = ContentComplexityScore.CreateFromMetadata(10, 5, 0.3m, false);

        // Assert
        score.ToString().Should().Contain("Complexity:");
        score.ToString().Should().Contain("pages=10");
        score.ToString().Should().Contain("tables=5");
    }

    [Fact]
    public void ComputeFromText_120KB_TextOnly_RoutesSynchronous()
    {
        // Acceptance criteria: 120KB text-only PDF routes to Synchronous
        var content = new string('A', 120000); // ~40 pages

        // Act
        var score = ContentComplexityScore.ComputeFromText(content);

        // Assert - 40 pages = 0.3 page component, no tables/images/ocr = 0.3 total < 0.4
        score.Decision.Should().Be(RoutingDecision.Synchronous);
    }
}
