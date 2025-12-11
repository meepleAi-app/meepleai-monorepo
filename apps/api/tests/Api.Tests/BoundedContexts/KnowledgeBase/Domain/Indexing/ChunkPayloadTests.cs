using Api.BoundedContexts.KnowledgeBase.Domain.Indexing;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Indexing;

/// <summary>
/// ADR-016 Phase 3: Unit tests for ChunkPayload value object.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ChunkPayloadTests
{
    private static readonly Guid TestGameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestPdfId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Empty_ReturnsEmptyPayload()
    {
        // Act
        var payload = ChunkPayload.Empty();

        // Assert
        Assert.Equal(string.Empty, payload.GameId);
        Assert.Equal(string.Empty, payload.PdfId);
        Assert.Equal(0, payload.Level);
        Assert.True(payload.IsRoot);
        Assert.False(payload.HasChildren);
    }

    [Fact]
    public void Create_WithValidValues_Succeeds()
    {
        // Act
        var payload = ChunkPayload.Create(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 5,
            chunkIndex: 3,
            level: 1,
            parentChunkId: "parent-123",
            category: "rules",
            language: "it",
            heading: "Game Setup",
            elementType: "NarrativeText",
            tokenCount: 250);

        // Assert
        Assert.Equal(TestGameId.ToString(), payload.GameId);
        Assert.Equal(TestPdfId.ToString(), payload.PdfId);
        Assert.Equal(5, payload.PageNumber);
        Assert.Equal(3, payload.ChunkIndex);
        Assert.Equal(1, payload.Level);
        Assert.Equal("parent-123", payload.ParentChunkId);
        Assert.Equal("rules", payload.Category);
        Assert.Equal("it", payload.Language);
        Assert.Equal("Game Setup", payload.Heading);
        Assert.Equal("NarrativeText", payload.ElementType);
        Assert.Equal(250, payload.TokenCount);
        Assert.False(payload.IsRoot);
    }

    [Fact]
    public void CreateRoot_WithNoParent_IsRoot()
    {
        // Act
        var payload = ChunkPayload.CreateRoot(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0,
            heading: "Chapter 1",
            elementType: "Title");

        // Assert
        Assert.Equal(0, payload.Level);
        Assert.Null(payload.ParentChunkId);
        Assert.True(payload.IsRoot);
    }

    [Fact]
    public void CreateChild_WithParent_IsNotRoot()
    {
        // Act
        var payload = ChunkPayload.CreateChild(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 2,
            chunkIndex: 5,
            level: 2,
            parentChunkId: "section-001",
            heading: "Subsection");

        // Assert
        Assert.Equal(2, payload.Level);
        Assert.Equal("section-001", payload.ParentChunkId);
        Assert.False(payload.IsRoot);
    }

    [Fact]
    public void CreateChild_WithEmptyParentId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ChunkPayload.CreateChild(
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                pageNumber: 1,
                chunkIndex: 0,
                level: 1,
                parentChunkId: ""));
    }

    [Fact]
    public void CreateChild_WithNullParentId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ChunkPayload.CreateChild(
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                pageNumber: 1,
                chunkIndex: 0,
                level: 1,
                parentChunkId: null!));
    }

    [Fact]
    public void WithChildren_ReturnsCopyWithChildren()
    {
        // Arrange
        var original = ChunkPayload.CreateRoot(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0);

        var childIds = new List<string> { "child-1", "child-2", "child-3" };

        // Act
        var updated = original.WithChildren(childIds);

        // Assert
        Assert.Equal(3, updated.ChildChunkIds.Count);
        Assert.True(updated.HasChildren);
        Assert.Contains("child-1", updated.ChildChunkIds);
        Assert.Contains("child-2", updated.ChildChunkIds);
        Assert.Contains("child-3", updated.ChildChunkIds);
    }

    [Fact]
    public void DefaultLanguage_IsItalian()
    {
        // Act
        var payload = ChunkPayload.Create(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0,
            level: 0);

        // Assert - Default language should be Italian per ADR-016 Phase 3
        Assert.Equal("it", payload.Language);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var payload1 = ChunkPayload.Create(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0,
            level: 0);

        var payload2 = ChunkPayload.Create(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0,
            level: 0);

        // Act & Assert
        Assert.Equal(payload1, payload2);
    }

    [Fact]
    public void Equality_DifferentChunkIndex_AreNotEqual()
    {
        // Arrange
        var payload1 = ChunkPayload.Create(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0,
            level: 0);

        var payload2 = ChunkPayload.Create(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 1,
            level: 0);

        // Act & Assert
        Assert.NotEqual(payload1, payload2);
    }

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var payload = ChunkPayload.Create(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 5,
            chunkIndex: 3,
            level: 1);

        // Act
        var result = payload.ToString();

        // Assert
        Assert.Contains("page=5", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("level=1", result, StringComparison.OrdinalIgnoreCase);
    }
}
