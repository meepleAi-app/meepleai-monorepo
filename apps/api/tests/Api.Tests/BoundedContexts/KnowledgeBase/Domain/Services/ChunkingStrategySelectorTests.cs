using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for ChunkingStrategySelector domain service.
/// </summary>
public class ChunkingStrategySelectorTests
{
    private readonly ChunkingStrategySelector _selector = new();

    [Fact]
    public void SelectStrategy_WithEmptyContent_ReturnsBaseline()
    {
        // Act
        var config = _selector.SelectStrategy("");

        // Assert
        config.Should().Be(ChunkingConfiguration.Baseline);
    }

    [Fact]
    public void SelectStrategy_WithNullContent_ReturnsBaseline()
    {
        // Act
        var config = _selector.SelectStrategy(null!);

        // Assert
        config.Should().Be(ChunkingConfiguration.Baseline);
    }

    [Fact]
    public void SelectStrategy_WithWhitespaceContent_ReturnsBaseline()
    {
        // Act
        var config = _selector.SelectStrategy("   ");

        // Assert
        config.Should().Be(ChunkingConfiguration.Baseline);
    }

    [Fact]
    public void SelectStrategy_WithTableElements_ReturnsDense()
    {
        // Arrange
        var content = "Some content with tables";
        var elementTypes = new[] { "table", "table", "text", "table" }; // 75% tables

        // Act
        var config = _selector.SelectStrategy(content, elementTypes);

        // Assert
        config.Should().Be(ChunkingConfiguration.Dense);
    }

    [Fact]
    public void SelectStrategy_WithListElements_ReturnsDense()
    {
        // Arrange
        var content = "Some content with lists";
        var elementTypes = new[] { "list", "list_item", "text", "list" }; // 75% list-related

        // Act
        var config = _selector.SelectStrategy(content, elementTypes);

        // Assert
        config.Should().Be(ChunkingConfiguration.Dense);
    }

    [Fact]
    public void SelectStrategy_WithTablePatterns_ReturnsDense()
    {
        // Arrange - Content with many pipe characters (Markdown tables)
        var content = @"
| Header 1 | Header 2 | Header 3 |
|----------|----------|----------|
| Cell 1   | Cell 2   | Cell 3   |
| Cell 4   | Cell 5   | Cell 6   |
";

        // Act
        var config = _selector.SelectStrategy(content);

        // Assert
        config.Should().Be(ChunkingConfiguration.Dense);
    }

    [Fact]
    public void SelectStrategy_WithBulletLists_ReturnsDense()
    {
        // Arrange
        var content = @"
Here is a list of items:
- First item in the list
- Second item in the list
- Third item in the list
- Fourth item in the list
- Fifth item in the list
";

        // Act
        var config = _selector.SelectStrategy(content);

        // Assert
        config.Should().Be(ChunkingConfiguration.Dense);
    }

    [Fact]
    public void SelectStrategy_WithNumberedLists_ReturnsDense()
    {
        // Arrange
        var content = @"
Setup instructions:
1. Open the box
2. Remove all components
3. Sort the cards
4. Read the rules
5. Begin playing
";

        // Act
        var config = _selector.SelectStrategy(content);

        // Assert
        config.Should().Be(ChunkingConfiguration.Dense);
    }

    [Fact]
    public void SelectStrategy_WithLongNarrativeContent_ReturnsSparse()
    {
        // Arrange - Long lines with minimal structural elements
        var content = string.Join("\n",
            Enumerable.Range(0, 10).Select(_ =>
                new string('x', 100) + " Some long narrative content that forms a paragraph."
            ));

        // Act
        var config = _selector.SelectStrategy(content);

        // Assert
        config.Should().Be(ChunkingConfiguration.Sparse);
    }

    [Fact]
    public void SelectStrategy_WithMixedContent_ReturnsBaseline()
    {
        // Arrange - Mix of short paragraphs and some structure
        var content = @"
Welcome to the game.

This is the first section with some general information.

Here are some rules:
The game is played with cards.

Enjoy!
";

        // Act
        var config = _selector.SelectStrategy(content);

        // Assert
        config.Should().Be(ChunkingConfiguration.Baseline);
    }

    [Fact]
    public void SelectStrategy_WithShortNarrativeContent_ReturnsBaseline()
    {
        // Arrange - Short content (less than 500 chars)
        var content = "This is a short narrative paragraph that doesn't warrant sparse chunking.";

        // Act
        var config = _selector.SelectStrategy(content);

        // Assert
        config.Should().Be(ChunkingConfiguration.Baseline);
    }

    [Fact]
    public void SelectStrategy_WithTextOnlyElements_UsesContentAnalysis()
    {
        // Arrange
        var content = "Simple text content without special patterns.";
        var elementTypes = new[] { "text", "text", "text" };

        // Act
        var config = _selector.SelectStrategy(content, elementTypes);

        // Assert
        config.Should().Be(ChunkingConfiguration.Baseline);
    }

    [Fact]
    public void SelectStrategy_WithTabSeparatedContent_ReturnsDense()
    {
        // Arrange - TSV-like content
        var content = "Col1\tCol2\tCol3\nVal1\tVal2\tVal3\nVal4\tVal5\tVal6";

        // Act
        var config = _selector.SelectStrategy(content);

        // Assert
        config.Should().Be(ChunkingConfiguration.Dense);
    }

    [Fact]
    public void SelectStrategy_PrioritizesElementTypesOverContent()
    {
        // Arrange - Simple content but table element types
        var content = "Simple text content";
        var elementTypes = new[] { "table", "table_cell", "table_row", "table" };

        // Act
        var config = _selector.SelectStrategy(content, elementTypes);

        // Assert
        config.Should().Be(ChunkingConfiguration.Dense);
    }
}
