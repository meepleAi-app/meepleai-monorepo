using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Unit tests for SemanticChunk value object.
/// Issue #2525: Background Rulebook Analysis Tests - Value Object Validation
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SemanticChunkTests
{
    #region Create - Happy Path

    [Fact]
    public void Create_WithValidData_ReturnsChunk()
    {
        // Arrange
        var chunkIndex = 0;
        var content = "This is test content for the semantic chunk.";
        var startCharIndex = 0;
        var endCharIndex = 45;
        var sectionHeader = "Setup";
        var contextHeaders = new List<string> { "Game Overview", "Setup" };

        // Act
        var chunk = SemanticChunk.Create(
            chunkIndex,
            content,
            startCharIndex,
            endCharIndex,
            sectionHeader,
            contextHeaders);

        // Assert
        chunk.Should().NotBeNull();
        chunk.ChunkIndex.Should().Be(0);
        chunk.Content.Should().Be(content);
        chunk.StartCharIndex.Should().Be(0);
        chunk.EndCharIndex.Should().Be(45);
        chunk.SectionHeader.Should().Be("Setup");
        chunk.ContextHeaders.Should().HaveCount(2);
        chunk.ContextHeaders.Should().Contain("Game Overview");
        chunk.ContextHeaders.Should().Contain("Setup");
    }

    [Fact]
    public void Create_WithoutOptionalParameters_ReturnsChunkWithDefaults()
    {
        // Arrange & Act
        var chunk = SemanticChunk.Create(
            chunkIndex: 1,
            content: "Content without headers",
            startCharIndex: 10,
            endCharIndex: 32);

        // Assert
        chunk.ChunkIndex.Should().Be(1);
        chunk.Content.Should().Be("Content without headers");
        chunk.SectionHeader.Should().BeNull();
        chunk.ContextHeaders.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithNullContextHeaders_UsesEmptyList()
    {
        // Arrange & Act
        var chunk = SemanticChunk.Create(
            chunkIndex: 0,
            content: "Test content",
            startCharIndex: 0,
            endCharIndex: 12,
            sectionHeader: "Header",
            contextHeaders: null);

        // Assert
        chunk.ContextHeaders.Should().NotBeNull();
        chunk.ContextHeaders.Should().BeEmpty();
    }

    #endregion

    #region Create - ChunkIndex Validation

    [Fact]
    public void Create_WithNegativeChunkIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidIndex = -1;

        // Act & Assert
        var act = () => SemanticChunk.Create(
            chunkIndex: invalidIndex,
            content: "Content",
            startCharIndex: 0,
            endCharIndex: 7);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("chunkIndex");
    }

    [Fact]
    public void Create_WithZeroChunkIndex_ReturnsChunk()
    {
        // Arrange & Act
        var chunk = SemanticChunk.Create(
            chunkIndex: 0,
            content: "First chunk",
            startCharIndex: 0,
            endCharIndex: 11);

        // Assert
        chunk.ChunkIndex.Should().Be(0);
    }

    [Fact]
    public void Create_WithLargeChunkIndex_ReturnsChunk()
    {
        // Arrange & Act
        var chunk = SemanticChunk.Create(
            chunkIndex: 999,
            content: "Last chunk",
            startCharIndex: 10000,
            endCharIndex: 10010);

        // Assert
        chunk.ChunkIndex.Should().Be(999);
    }

    #endregion

    #region Create - Content Validation

    [Fact]
    public void Create_WithNullContent_ThrowsArgumentException()
    {
        // Arrange
        string? nullContent = null;

        // Act & Assert
        var act = () => SemanticChunk.Create(
            chunkIndex: 0,
            content: nullContent!,
            startCharIndex: 0,
            endCharIndex: 0);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("content");
    }

    [Fact]
    public void Create_WithEmptyContent_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => SemanticChunk.Create(
            chunkIndex: 0,
            content: string.Empty,
            startCharIndex: 0,
            endCharIndex: 0);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("content");
    }

    [Fact]
    public void Create_WithWhitespaceOnlyContent_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => SemanticChunk.Create(
            chunkIndex: 0,
            content: "   \n\t\r\n   ",
            startCharIndex: 0,
            endCharIndex: 10);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("content");
    }

    [Fact]
    public void Create_WithValidContent_ReturnsChunk()
    {
        // Arrange & Act
        var chunk = SemanticChunk.Create(
            chunkIndex: 0,
            content: "Valid content with text",
            startCharIndex: 0,
            endCharIndex: 23);

        // Assert
        chunk.Content.Should().Be("Valid content with text");
    }

    #endregion

    #region Create - Character Index Validation

    [Fact]
    public void Create_WithNegativeStartCharIndex_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => SemanticChunk.Create(
            chunkIndex: 0,
            content: "Content",
            startCharIndex: -1,
            endCharIndex: 7);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("startCharIndex");
    }

    [Fact]
    public void Create_WithEndCharIndexLessThanStart_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => SemanticChunk.Create(
            chunkIndex: 0,
            content: "Content",
            startCharIndex: 10,
            endCharIndex: 5); // Less than start

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("endCharIndex");
    }

    [Fact]
    public void Create_WithStartEqualToEnd_ReturnsChunk()
    {
        // Arrange & Act
        var chunk = SemanticChunk.Create(
            chunkIndex: 0,
            content: "X",
            startCharIndex: 5,
            endCharIndex: 5);

        // Assert
        chunk.StartCharIndex.Should().Be(5);
        chunk.EndCharIndex.Should().Be(5);
    }

    [Fact]
    public void Create_WithValidCharacterRange_ReturnsChunk()
    {
        // Arrange & Act
        var chunk = SemanticChunk.Create(
            chunkIndex: 2,
            content: "Middle section content",
            startCharIndex: 1000,
            endCharIndex: 1022);

        // Assert
        chunk.StartCharIndex.Should().Be(1000);
        chunk.EndCharIndex.Should().Be(1022);
    }

    #endregion

    #region CharacterCount Property

    [Fact]
    public void CharacterCount_ReturnsContentLength()
    {
        // Arrange
        var content = "This content has 31 characters";
        var chunk = SemanticChunk.Create(
            chunkIndex: 0,
            content: content,
            startCharIndex: 0,
            endCharIndex: 100);

        // Act
        var count = chunk.CharacterCount;

        // Assert
        count.Should().Be(content.Length);
        count.Should().Be(30); // Actual length of "This content has 31 characters"
    }

    [Fact]
    public void CharacterCount_WithLongContent_ReturnsCorrectLength()
    {
        // Arrange
        var content = new string('A', 10000);
        var chunk = SemanticChunk.Create(
            chunkIndex: 0,
            content: content,
            startCharIndex: 0,
            endCharIndex: 10000);

        // Act
        var count = chunk.CharacterCount;

        // Assert
        count.Should().Be(10000);
    }

    #endregion

    #region Metadata Consistency

    [Fact]
    public void Create_PreservesAllMetadata()
    {
        // Arrange
        var chunkIndex = 5;
        var content = "Detailed game mechanics explanation.";
        var startCharIndex = 5000;
        var endCharIndex = 5036;
        var sectionHeader = "Game Mechanics";
        var contextHeaders = new List<string> { "Overview", "Setup", "Game Mechanics" };

        // Act
        var chunk = SemanticChunk.Create(
            chunkIndex,
            content,
            startCharIndex,
            endCharIndex,
            sectionHeader,
            contextHeaders);

        // Assert - All properties preserved exactly
        chunk.ChunkIndex.Should().Be(chunkIndex);
        chunk.Content.Should().Be(content);
        chunk.StartCharIndex.Should().Be(startCharIndex);
        chunk.EndCharIndex.Should().Be(endCharIndex);
        chunk.SectionHeader.Should().Be(sectionHeader);
        chunk.ContextHeaders.Should().HaveCount(3);
        chunk.ContextHeaders.Should().BeEquivalentTo(contextHeaders);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithEmptySectionHeader_AcceptsNullableBehavior()
    {
        // Arrange & Act
        var chunk = SemanticChunk.Create(
            chunkIndex: 0,
            content: "Content",
            startCharIndex: 0,
            endCharIndex: 7,
            sectionHeader: string.Empty);

        // Assert - Empty string is valid for nullable string
        chunk.SectionHeader.Should().Be(string.Empty);
    }

    [Fact]
    public void Create_WithDuplicateContextHeaders_PreservesDuplicates()
    {
        // Arrange
        var contextHeaders = new List<string> { "Setup", "Setup", "Gameplay" };

        // Act
        var chunk = SemanticChunk.Create(
            chunkIndex: 0,
            content: "Content",
            startCharIndex: 0,
            endCharIndex: 7,
            contextHeaders: contextHeaders);

        // Assert - Duplicates are preserved (no automatic deduplication)
        chunk.ContextHeaders.Should().HaveCount(3);
        chunk.ContextHeaders.Should().ContainInOrder("Setup", "Setup", "Gameplay");
    }

    [Fact]
    public void Create_WithUnicodeContent_HandlesCorrectly()
    {
        // Arrange
        var unicodeContent = "Test with emojis 🎲🎯 and special chars: ñ, é, 中文";

        // Act
        var chunk = SemanticChunk.Create(
            chunkIndex: 0,
            content: unicodeContent,
            startCharIndex: 0,
            endCharIndex: 100);

        // Assert
        chunk.Content.Should().Be(unicodeContent);
        chunk.CharacterCount.Should().Be(unicodeContent.Length);
    }

    #endregion

    #region Record Equality

    [Fact]
    public void Equality_WithIdenticalValues_AreEquivalent()
    {
        // Arrange
        var chunk1 = SemanticChunk.Create(0, "Content", 0, 7);
        var chunk2 = SemanticChunk.Create(0, "Content", 0, 7);

        // Assert - Use BeEquivalentTo for value comparison
        chunk1.Should().BeEquivalentTo(chunk2);
    }

    [Fact]
    public void Equality_WithDifferentContent_AreNotEquivalent()
    {
        // Arrange
        var chunk1 = SemanticChunk.Create(0, "Content A", 0, 9);
        var chunk2 = SemanticChunk.Create(0, "Content B", 0, 9);

        // Assert
        chunk1.Should().NotBeEquivalentTo(chunk2);
    }

    #endregion
}