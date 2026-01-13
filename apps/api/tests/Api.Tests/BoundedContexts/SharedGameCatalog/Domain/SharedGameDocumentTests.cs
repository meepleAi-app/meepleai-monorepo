using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
public class SharedGameDocumentTests
{
    private static readonly Guid TestGameId = Guid.NewGuid();
    private static readonly Guid TestPdfId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_CreatesDocumentSuccessfully()
    {
        // Arrange & Act
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Rulebook,
            "1.0",
            TestUserId);

        // Assert
        Assert.NotEqual(Guid.Empty, document.Id);
        Assert.Equal(TestGameId, document.SharedGameId);
        Assert.Equal(TestPdfId, document.PdfDocumentId);
        Assert.Equal(SharedGameDocumentType.Rulebook, document.DocumentType);
        Assert.Equal("1.0", document.Version);
        Assert.False(document.IsActive); // Not active by default
        Assert.Empty(document.Tags);
        Assert.Equal(TestUserId, document.CreatedBy);
    }

    [Fact]
    public void Create_WithHomeruleAndTags_AddsTags()
    {
        // Arrange
        var tags = new[] { "speed-mode", "2-players" };

        // Act
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Homerule,
            "1.0",
            TestUserId,
            tags);

        // Assert
        Assert.Equal(2, document.Tags.Count);
        Assert.Contains("speed-mode", document.Tags);
        Assert.Contains("2-players", document.Tags);
    }

    [Fact]
    public void Create_WithInvalidVersion_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SharedGameDocument.Create(
                TestGameId,
                TestPdfId,
                SharedGameDocumentType.Rulebook,
                "invalid",
                TestUserId));
    }

    [Fact]
    public void SetAsActive_SetsIsActiveToTrue()
    {
        // Arrange
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Rulebook,
            "1.0",
            TestUserId);

        // Act
        document.SetAsActive();

        // Assert
        Assert.True(document.IsActive);
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        // Arrange
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Rulebook,
            "1.0",
            TestUserId);
        document.SetAsActive();

        // Act
        document.Deactivate();

        // Assert
        Assert.False(document.IsActive);
    }

    [Fact]
    public void AddTag_ToHomerule_AddsTagSuccessfully()
    {
        // Arrange
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Homerule,
            "1.0",
            TestUserId);

        // Act
        document.AddTag("family-friendly");

        // Assert
        Assert.Single(document.Tags);
        Assert.Contains("family-friendly", document.Tags);
    }

    [Fact]
    public void AddTag_ToNonHomerule_ThrowsException()
    {
        // Arrange
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Rulebook,
            "1.0",
            TestUserId);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            document.AddTag("some-tag"));
        Assert.Contains("only allowed for Homerule", ex.Message);
    }

    [Fact]
    public void AddTag_NormalizesTag()
    {
        // Arrange
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Homerule,
            "1.0",
            TestUserId);

        // Act
        document.AddTag("SPEED Mode!");

        // Assert
        Assert.Single(document.Tags);
        Assert.Contains("speed-mode", document.Tags);
    }

    [Fact]
    public void AddTag_DuplicateTag_DoesNotAddTwice()
    {
        // Arrange
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Homerule,
            "1.0",
            TestUserId);

        // Act
        document.AddTag("speed-mode");
        document.AddTag("speed-mode");

        // Assert
        Assert.Single(document.Tags);
    }

    [Fact]
    public void AddTag_MoreThan10Tags_ThrowsException()
    {
        // Arrange
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Homerule,
            "1.0",
            TestUserId);

        // Add 10 tags
        for (int i = 0; i < 10; i++)
        {
            document.AddTag($"tag-{i}");
        }

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            document.AddTag("tag-11"));
        Assert.Contains("Cannot add more than 10 tags", ex.Message);
    }

    [Fact]
    public void RemoveTag_RemovesTagSuccessfully()
    {
        // Arrange
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Homerule,
            "1.0",
            TestUserId);
        document.AddTag("speed-mode");

        // Act
        document.RemoveTag("speed-mode");

        // Assert
        Assert.Empty(document.Tags);
    }

    [Fact]
    public void HasTag_ReturnsTrueIfTagExists()
    {
        // Arrange
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Homerule,
            "1.0",
            TestUserId);
        document.AddTag("speed-mode");

        // Act & Assert
        Assert.True(document.HasTag("speed-mode"));
        Assert.False(document.HasTag("family-friendly"));
    }

    [Fact]
    public void ClearTags_RemovesAllTags()
    {
        // Arrange
        var document = SharedGameDocument.Create(
            TestGameId,
            TestPdfId,
            SharedGameDocumentType.Homerule,
            "1.0",
            TestUserId,
            new[] { "tag1", "tag2", "tag3" });

        // Act
        document.ClearTags();

        // Assert
        Assert.Empty(document.Tags);
    }
}
