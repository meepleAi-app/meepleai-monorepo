using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Unit tests for PdfDocument aggregate root (Issue #2029 focus).
/// Tests Language property and UpdateLanguage method.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PdfDocumentTests
{
    [Fact]
    public void Constructor_WithDefaultLanguage_SetsEnglish()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fileName = new FileName("test.pdf");
        var fileSize = new FileSize(1024);

        // Act
        var document = new PdfDocument(
            id,
            gameId,
            fileName,
            "/path/to/test.pdf",
            fileSize,
            userId
        );

        // Assert
        Assert.Equal(LanguageCode.English.Value, document.Language.Value);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("fr")]
    public void Constructor_WithSpecifiedLanguage_SetsCorrectly(string languageCode)
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fileName = new FileName("test.pdf");
        var fileSize = new FileSize(1024);
        var language = new LanguageCode(languageCode);

        // Act
        var document = new PdfDocument(
            id,
            gameId,
            fileName,
            "/path/to/test.pdf",
            fileSize,
            userId,
            language
        );

        // Assert
        Assert.Equal(languageCode, document.Language.Value);
    }

    [Fact]
    public void UpdateLanguage_WithValidLanguage_UpdatesSuccessfully()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);
        var newLanguage = LanguageCode.Italian;

        // Act
        document.UpdateLanguage(newLanguage);

        // Assert
        Assert.Equal("it", document.Language.Value);
    }

    [Theory]
    [InlineData("en", "it")]
    [InlineData("it", "de")]
    [InlineData("de", "fr")]
    [InlineData("fr", "es")]
    public void UpdateLanguage_WithDifferentLanguages_UpdatesCorrectly(string initial, string updated)
    {
        // Arrange
        var document = CreateTestDocument(new LanguageCode(initial));
        var newLanguage = new LanguageCode(updated);

        // Act
        document.UpdateLanguage(newLanguage);

        // Assert
        Assert.Equal(updated, document.Language.Value);
    }

    [Fact]
    public void UpdateLanguage_WithNullLanguage_ThrowsArgumentNullException()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => document.UpdateLanguage(null!));
    }

    [Fact]
    public void UpdateLanguage_MultipleUpdates_MaintainsLastValue()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act
        document.UpdateLanguage(LanguageCode.Italian);
        document.UpdateLanguage(LanguageCode.German);
        document.UpdateLanguage(LanguageCode.French);

        // Assert
        Assert.Equal("fr", document.Language.Value);
    }

    [Fact]
    public void UpdateLanguage_AfterProcessing_AllowsUpdate()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);
        document.MarkAsProcessing();
        document.MarkAsCompleted(10);

        // Act
        document.UpdateLanguage(LanguageCode.Italian);

        // Assert
        Assert.Equal("it", document.Language.Value);
        Assert.Equal("completed", document.ProcessingStatus);
    }

    // ===== LinkToGame Tests (Issue #3372) =====

    [Fact]
    public void LinkToGame_WithValidGameId_UpdatesGameId()
    {
        // Arrange
        var originalGameId = Guid.NewGuid();
        var newGameId = Guid.NewGuid();
        var document = CreateTestDocumentWithGameId(originalGameId);

        // Act
        document.LinkToGame(newGameId);

        // Assert
        Assert.Equal(newGameId, document.GameId);
    }

    [Fact]
    public void LinkToGame_WithEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => document.LinkToGame(Guid.Empty));

        Assert.Contains("Game ID cannot be empty", exception.Message);
        Assert.Equal("gameId", exception.ParamName);
    }

    [Fact]
    public void LinkToGame_OverwritesExistingGameId()
    {
        // Arrange
        var firstGameId = Guid.NewGuid();
        var secondGameId = Guid.NewGuid();
        var document = CreateTestDocumentWithGameId(firstGameId);

        // Verify initial state
        Assert.Equal(firstGameId, document.GameId);

        // Act - Link to second game
        document.LinkToGame(secondGameId);

        // Assert - GameId should be overwritten
        Assert.Equal(secondGameId, document.GameId);
        Assert.NotEqual(firstGameId, document.GameId);
    }

    [Fact]
    public void LinkToGame_WithSameGameId_DoesNotThrow()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var document = CreateTestDocumentWithGameId(gameId);

        // Act - Link to same game (idempotent operation)
        document.LinkToGame(gameId);

        // Assert
        Assert.Equal(gameId, document.GameId);
    }

    private static PdfDocument CreateTestDocument(LanguageCode language)
    {
        return new PdfDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "/path/to/test.pdf",
            new FileSize(1024),
            Guid.NewGuid(),
            language
        );
    }

    private static PdfDocument CreateTestDocumentWithGameId(Guid gameId)
    {
        return new PdfDocument(
            Guid.NewGuid(),
            gameId,
            new FileName("test.pdf"),
            "/path/to/test.pdf",
            new FileSize(1024),
            Guid.NewGuid(),
            LanguageCode.English
        );
    }
}
