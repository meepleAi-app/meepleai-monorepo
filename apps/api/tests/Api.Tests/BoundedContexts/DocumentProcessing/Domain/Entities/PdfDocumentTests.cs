using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
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

    // ===== Retry Logic Tests (Issue #4216) =====

    [Fact]
    public void CanRetry_WhenFailedAndBelowMaxRetries_ReturnsTrue()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);
        document.MarkAsFailed("Test error", ErrorCategory.Network, PdfProcessingState.Extracting);

        // Act
        var canRetry = document.CanRetry();

        // Assert
        Assert.True(canRetry);
        Assert.Equal(0, document.RetryCount);
        Assert.Equal(PdfProcessingState.Failed, document.ProcessingState);
    }

    [Fact]
    public void CanRetry_WhenAtMaxRetries_ReturnsFalse()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);
        document.MarkAsFailed("Error 1", ErrorCategory.Network, PdfProcessingState.Extracting);
        document.Retry(); // Retry 1
        document.MarkAsFailed("Error 2", ErrorCategory.Parsing, PdfProcessingState.Chunking);
        document.Retry(); // Retry 2
        document.MarkAsFailed("Error 3", ErrorCategory.Service, PdfProcessingState.Embedding);
        document.Retry(); // Retry 3
        document.MarkAsFailed("Error 4", ErrorCategory.Unknown, PdfProcessingState.Indexing);

        // Act
        var canRetry = document.CanRetry();

        // Assert
        Assert.False(canRetry);
        Assert.Equal(3, document.RetryCount);
        Assert.Equal(document.MaxRetries, document.RetryCount);
    }

    [Fact]
    public void CanRetry_WhenNotFailed_ReturnsFalse()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act
        var canRetry = document.CanRetry();

        // Assert
        Assert.False(canRetry);
        Assert.Equal(PdfProcessingState.Pending, document.ProcessingState);
    }

    [Fact]
    public void Retry_WhenAllowed_IncrementsCountAndResetsState()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);
        document.MarkAsFailed("Network timeout", ErrorCategory.Network, PdfProcessingState.Extracting);

        // Act
        document.Retry();

        // Assert
        Assert.Equal(1, document.RetryCount);
        Assert.Equal(PdfProcessingState.Extracting, document.ProcessingState);
        Assert.Null(document.ProcessingError);
        Assert.Null(document.ProcessedAt);
    }

    [Fact]
    public void Retry_WhenAtMaxRetries_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Exhaust retries
        for (int i = 0; i < 3; i++)
        {
            document.MarkAsFailed($"Error {i + 1}", ErrorCategory.Network, PdfProcessingState.Extracting);
            document.Retry();
        }

        // Final failure
        document.MarkAsFailed("Final error", ErrorCategory.Unknown, PdfProcessingState.Indexing);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => document.Retry());
        Assert.Contains("Cannot retry", exception.Message);
        Assert.Contains("RetryCount=3", exception.Message);
    }

    [Fact]
    public void Retry_WhenNotFailed_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => document.Retry());
        Assert.Contains("Cannot retry", exception.Message);
        Assert.Contains("State=Pending", exception.Message);
    }

    [Fact]
    public void Retry_ResumesFromFailedAtState()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);
        document.MarkAsFailed("Chunking failed", ErrorCategory.Parsing, PdfProcessingState.Chunking);

        // Act
        document.Retry();

        // Assert - Should resume from Chunking, not restart from beginning
        Assert.Equal(PdfProcessingState.Chunking, document.ProcessingState);
    }

    [Fact]
    public void MarkAsFailed_WithCategory_SetsAllFields()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);
        var errorMessage = "Network timeout after 30s";
        var category = ErrorCategory.Network;
        var failedState = PdfProcessingState.Extracting;

        // Act
        document.MarkAsFailed(errorMessage, category, failedState);

        // Assert
        Assert.Equal(PdfProcessingState.Failed, document.ProcessingState);
        Assert.Equal(errorMessage, document.ProcessingError);
        Assert.Equal(category, document.ErrorCategory);
        Assert.Equal(failedState, document.FailedAtState);
        Assert.NotNull(document.ProcessedAt);
    }

    [Theory]
    [InlineData(ErrorCategory.Network)]
    [InlineData(ErrorCategory.Parsing)]
    [InlineData(ErrorCategory.Quota)]
    [InlineData(ErrorCategory.Service)]
    [InlineData(ErrorCategory.Unknown)]
    public void MarkAsFailed_WithDifferentCategories_SetsCorrectly(ErrorCategory category)
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act
        document.MarkAsFailed("Test error", category, PdfProcessingState.Embedding);

        // Assert
        Assert.Equal(category, document.ErrorCategory);
    }

    [Fact]
    public void MaxRetries_Returns3()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act & Assert
        Assert.Equal(3, document.MaxRetries);
    }

    [Fact]
    public void RetryWorkflow_FullCycle_WorksCorrectly()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Scenario: Network error → retry → parsing error → retry → success

        // First failure
        document.MarkAsFailed("Network error", ErrorCategory.Network, PdfProcessingState.Extracting);
        Assert.True(document.CanRetry());
        Assert.Equal(0, document.RetryCount);

        // First retry
        document.Retry();
        Assert.Equal(1, document.RetryCount);
        Assert.Equal(PdfProcessingState.Extracting, document.ProcessingState);
        Assert.Null(document.ProcessingError);

        // Second failure
        document.MarkAsFailed("Parsing error", ErrorCategory.Parsing, PdfProcessingState.Chunking);
        Assert.True(document.CanRetry());

        // Second retry
        document.Retry();
        Assert.Equal(2, document.RetryCount);
        Assert.Equal(PdfProcessingState.Chunking, document.ProcessingState);

        // Success path - complete processing
        document.MarkAsCompleted(10);
        Assert.Equal(PdfProcessingState.Ready, document.ProcessingState);
        Assert.False(document.CanRetry()); // Can't retry completed document
    }
}
