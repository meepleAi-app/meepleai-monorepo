using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Unit tests for VectorDocument aggregate root.
/// Issue #2639: KnowledgeBase test suite
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class VectorDocumentTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesVectorDocument()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var language = "en";
        var totalChunks = 10;

        // Act
        var document = new VectorDocument(id, gameId, pdfDocumentId, language, totalChunks);

        // Assert
        Assert.Equal(id, document.Id);
        Assert.Equal(gameId, document.GameId);
        Assert.Equal(pdfDocumentId, document.PdfDocumentId);
        Assert.Equal("en", document.Language);
        Assert.Equal(10, document.TotalChunks);
        Assert.Equal(0, document.SearchCount);
        Assert.Null(document.LastSearchedAt);
        Assert.Null(document.Metadata);
    }

    [Fact]
    public void Constructor_LanguageWithUpperCase_NormalizesToLowerCase()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act
        var document = new VectorDocument(id, gameId, pdfDocumentId, "EN-US", 5);

        // Assert
        Assert.Equal("en-us", document.Language);
    }

    [Fact]
    public void Constructor_EmptyLanguage_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new VectorDocument(id, gameId, pdfDocumentId, "", 10));
        Assert.Contains("Language cannot be empty", exception.Message);
    }

    [Fact]
    public void Constructor_WhitespaceLanguage_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new VectorDocument(id, gameId, pdfDocumentId, "   ", 10));
    }

    [Fact]
    public void Constructor_ZeroChunks_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new VectorDocument(id, gameId, pdfDocumentId, "en", 0));
        Assert.Contains("Total chunks must be positive", exception.Message);
    }

    [Fact]
    public void Constructor_NegativeChunks_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new VectorDocument(id, gameId, pdfDocumentId, "en", -5));
    }

    [Fact]
    public void RecordSearch_ValidQuery_IncrementsSearchCountAndUpdatesTimestamp()
    {
        // Arrange
        var document = CreateTestDocument();
        var beforeSearch = DateTime.UtcNow;

        // Act
        document.RecordSearch("test query");

        // Assert
        Assert.Equal(1, document.SearchCount);
        Assert.NotNull(document.LastSearchedAt);
        Assert.True(document.LastSearchedAt >= beforeSearch);
        Assert.True(document.LastSearchedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void RecordSearch_MultipleSearches_IncrementsCount()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        document.RecordSearch("query 1");
        document.RecordSearch("query 2");
        document.RecordSearch("query 3");

        // Assert
        Assert.Equal(3, document.SearchCount);
    }

    [Fact]
    public void UpdateMetadata_ValidMetadata_SetsMetadataValue()
    {
        // Arrange
        var document = CreateTestDocument();
        var metadata = "{\"quality\": 0.85, \"views\": 100}";

        // Act
        document.UpdateMetadata(metadata);

        // Assert
        Assert.Equal(metadata, document.Metadata);
    }

    [Fact]
    public void UpdateMetadata_MultipleTimes_OverwritesPreviousValue()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        document.UpdateMetadata("{\"version\": 1}");
        document.UpdateMetadata("{\"version\": 2}");

        // Assert
        Assert.Equal("{\"version\": 2}", document.Metadata);
    }

    [Fact]
    public void SetMetadata_NullValue_SetsToNull()
    {
        // Arrange
        var document = CreateTestDocument();
        document.UpdateMetadata("{\"test\": true}");

        // Act
        document.SetMetadata(null);

        // Assert
        Assert.Null(document.Metadata);
    }

    [Fact]
    public void Constructor_SetsIndexedAtToCurrentTime()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var document = CreateTestDocument();

        // Assert
        Assert.True(document.IndexedAt >= beforeCreation);
        Assert.True(document.IndexedAt <= DateTime.UtcNow);
    }

    // Helper method
    private static VectorDocument CreateTestDocument(
        string language = "en",
        int totalChunks = 10)
    {
        return new VectorDocument(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: language,
            totalChunks: totalChunks
        );
    }
}
