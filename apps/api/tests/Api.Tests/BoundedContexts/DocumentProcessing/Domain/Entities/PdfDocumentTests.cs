using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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
        document.Language.Value.Should().Be(LanguageCode.English.Value);
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
        document.Language.Value.Should().Be(languageCode);
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
        document.Language.Value.Should().Be("it");
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
        document.Language.Value.Should().Be(updated);
    }

    [Fact]
    public void UpdateLanguage_WithNullLanguage_ThrowsArgumentNullException()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act & Assert
        ((Action)(() => document.UpdateLanguage(null!))).Should().Throw<ArgumentNullException>();
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
        document.Language.Value.Should().Be("fr");
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
        document.Language.Value.Should().Be("it");
        document.ProcessingState.Should().Be(PdfProcessingState.Ready);
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
        document.GameId.Should().Be(newGameId);
    }

    [Fact]
    public void LinkToGame_WithEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act & Assert
        var act = () => document.LinkToGame(Guid.Empty);
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.Message.Should().Contain("Game ID cannot be empty");
        exception.ParamName.Should().Be("gameId");
    }

    [Fact]
    public void LinkToGame_OverwritesExistingGameId()
    {
        // Arrange
        var firstGameId = Guid.NewGuid();
        var secondGameId = Guid.NewGuid();
        var document = CreateTestDocumentWithGameId(firstGameId);

        // Verify initial state
        document.GameId.Should().Be(firstGameId);

        // Act - Link to second game
        document.LinkToGame(secondGameId);

        // Assert - GameId should be overwritten
        document.GameId.Should().Be(secondGameId);
        document.GameId.Should().NotBe(firstGameId);
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
        document.GameId.Should().Be(gameId);
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
        canRetry.Should().BeTrue();
        document.RetryCount.Should().Be(0);
        document.ProcessingState.Should().Be(PdfProcessingState.Failed);
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
        canRetry.Should().BeFalse();
        document.RetryCount.Should().Be(3);
        document.RetryCount.Should().Be(document.MaxRetries);
    }

    [Fact]
    public void CanRetry_WhenNotFailed_ReturnsFalse()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act
        var canRetry = document.CanRetry();

        // Assert
        canRetry.Should().BeFalse();
        document.ProcessingState.Should().Be(PdfProcessingState.Pending);
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
        document.RetryCount.Should().Be(1);
        document.ProcessingState.Should().Be(PdfProcessingState.Extracting);
        document.ProcessingError.Should().BeNull();
        document.ProcessedAt.Should().BeNull();
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
        var exception = ((Action)(() => document.Retry())).Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Cannot retry");
        exception.Message.Should().Contain("RetryCount=3");
    }

    [Fact]
    public void Retry_WhenNotFailed_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act & Assert
        var exception = ((Action)(() => document.Retry())).Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Cannot retry");
        exception.Message.Should().Contain("State=Pending");
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
        document.ProcessingState.Should().Be(PdfProcessingState.Chunking);
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
        document.ProcessingState.Should().Be(PdfProcessingState.Failed);
        document.ProcessingError.Should().Be(errorMessage);
        document.ErrorCategory.Should().Be(category);
        document.FailedAtState.Should().Be(failedState);
        document.ProcessedAt.Should().NotBeNull();
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
        document.ErrorCategory.Should().Be(category);
    }

    [Fact]
    public void MaxRetries_Returns3()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Act & Assert
        document.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void RetryWorkflow_FullCycle_WorksCorrectly()
    {
        // Arrange
        var document = CreateTestDocument(LanguageCode.English);

        // Scenario: Network error → retry → parsing error → retry → success

        // First failure
        document.MarkAsFailed("Network error", ErrorCategory.Network, PdfProcessingState.Extracting);
        document.CanRetry().Should().BeTrue();
        document.RetryCount.Should().Be(0);

        // First retry
        document.Retry();
        document.RetryCount.Should().Be(1);
        document.ProcessingState.Should().Be(PdfProcessingState.Extracting);
        document.ProcessingError.Should().BeNull();

        // Second failure
        document.MarkAsFailed("Parsing error", ErrorCategory.Parsing, PdfProcessingState.Chunking);
        document.CanRetry().Should().BeTrue();

        // Second retry
        document.Retry();
        document.RetryCount.Should().Be(2);
        document.ProcessingState.Should().Be(PdfProcessingState.Chunking);

        // Success path - complete processing through state machine
        document.TransitionTo(PdfProcessingState.Embedding);
        document.TransitionTo(PdfProcessingState.Indexing);
        document.MarkAsCompleted(10);
        document.ProcessingState.Should().Be(PdfProcessingState.Ready);
        document.CanRetry().Should().BeFalse(); // Can't retry completed document
    }

    #region BaseDocumentId Tests (Issue #5444)

    [Fact]
    public void Constructor_DefaultBaseDocumentId_IsNull()
    {
        var document = CreateDefaultDocument();
        document.BaseDocumentId.Should().BeNull();
    }

    [Fact]
    public void LinkToBaseDocument_WithValidId_SetsBaseDocumentId()
    {
        var document = CreateDefaultDocument();
        var baseDocId = Guid.NewGuid();

        document.LinkToBaseDocument(baseDocId);

        document.BaseDocumentId.Should().Be(baseDocId);
    }

    [Fact]
    public void LinkToBaseDocument_WithEmptyGuid_ThrowsArgumentException()
    {
        var document = CreateDefaultDocument();

        var act2 = () =>
            document.LinkToBaseDocument(Guid.Empty);
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkToBaseDocument_WithSelfReference_ThrowsArgumentException()
    {
        var document = CreateDefaultDocument();

        var act3 = () =>
            document.LinkToBaseDocument(document.Id);
        act3.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnlinkBaseDocument_ClearsBaseDocumentId()
    {
        var document = CreateDefaultDocument();
        var baseDocId = Guid.NewGuid();
        document.LinkToBaseDocument(baseDocId);

        document.UnlinkBaseDocument();

        document.BaseDocumentId.Should().BeNull();
    }

    [Fact]
    public void Reconstitute_WithBaseDocumentId_PreservesValue()
    {
        var baseDocId = Guid.NewGuid();
        var document = PdfDocument.Reconstitute(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            fileName: new FileName("test.pdf"),
            filePath: "/path/test.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow,
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English,
            baseDocumentId: baseDocId);

        document.BaseDocumentId.Should().Be(baseDocId);
    }

    [Fact]
    public void Reconstitute_WithoutBaseDocumentId_DefaultsToNull()
    {
        var document = PdfDocument.Reconstitute(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            fileName: new FileName("test.pdf"),
            filePath: "/path/test.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow,
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English);

        document.BaseDocumentId.Should().BeNull();
    }

    private static PdfDocument CreateDefaultDocument()
    {
        return new PdfDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "/path/to/test.pdf",
            new FileSize(1024),
            Guid.NewGuid());
    }

    #endregion
}
