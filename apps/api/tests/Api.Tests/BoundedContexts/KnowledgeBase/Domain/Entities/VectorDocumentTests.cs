using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Xunit;
using FluentAssertions;
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
        document.Id.Should().Be(id);
        document.GameId.Should().Be(gameId);
        document.PdfDocumentId.Should().Be(pdfDocumentId);
        document.Language.Should().Be("en");
        document.TotalChunks.Should().Be(10);
        document.SearchCount.Should().Be(0);
        document.LastSearchedAt.Should().BeNull();
        document.Metadata.Should().BeNull();
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
        document.Language.Should().Be("en-us");
    }

    [Fact]
    public void Constructor_EmptyLanguage_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        Action act = () =>
            new VectorDocument(id, gameId, pdfDocumentId, "", 10);
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().Contain("Language cannot be empty");
    }

    [Fact]
    public void Constructor_WhitespaceLanguage_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        Action act = () =>
            new VectorDocument(id, gameId, pdfDocumentId, "   ", 10);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ZeroChunks_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        Action act = () =>
            new VectorDocument(id, gameId, pdfDocumentId, "en", 0);
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().Contain("Total chunks must be positive");
    }

    [Fact]
    public void Constructor_NegativeChunks_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        Action act = () =>
            new VectorDocument(id, gameId, pdfDocumentId, "en", -5);
        act.Should().Throw<ArgumentException>();
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
        document.SearchCount.Should().Be(1);
        document.LastSearchedAt.Should().NotBeNull();
        (document.LastSearchedAt >= beforeSearch).Should().BeTrue();
        (document.LastSearchedAt <= DateTime.UtcNow).Should().BeTrue();
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
        document.SearchCount.Should().Be(3);
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
        document.Metadata.Should().Be(metadata);
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
        document.Metadata.Should().Be("{\"version\": 2}");
    }

    [Fact]
    public void Constructor_SetsIndexedAtToCurrentTime()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var document = CreateTestDocument();

        // Assert
        (document.IndexedAt >= beforeCreation).Should().BeTrue();
        (document.IndexedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    // ── SharedGameId (Issue #5185) ────────────────────────────────────────────

    [Fact]
    public void Constructor_WithoutSharedGameId_DefaultsToNull()
    {
        var document = CreateTestDocument();

        document.SharedGameId.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithSharedGameId_SetsSharedGameId()
    {
        var sharedGameId = Guid.NewGuid();

        var document = new VectorDocument(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 10,
            sharedGameId: sharedGameId);

        document.SharedGameId.Should().Be(sharedGameId);
    }

    // ── #2284 factory / rehydrate / TotalCharacters tests ────────────────────

    [Fact]
    public void Create_WithValidArgs_RaisesVectorDocumentIndexedEventOnce()
    {
        var doc = VectorDocument.Create(
            pdfDocumentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            totalChunks: 42,
            language: "en",
            sharedGameId: Guid.NewGuid());

        doc.DomainEvents.OfType<VectorDocumentIndexedEvent>().Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithTotalCharacters_SetsProperty()
    {
        var doc = VectorDocument.Create(
            pdfDocumentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            totalChunks: 5,
            language: "en",
            totalCharacters: 1234);

        doc.TotalCharacters.Should().Be(1234);
    }

    [Fact]
    public void Create_WithoutTotalCharacters_DefaultsToZero()
    {
        var doc = VectorDocument.Create(
            pdfDocumentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            totalChunks: 5,
            language: "en");

        doc.TotalCharacters.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNegativeTotalCharacters_Throws()
    {
        var act = () => new VectorDocument(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 5,
            sharedGameId: null,
            totalCharacters: -1);

        act.Should().Throw<ArgumentException>().WithMessage("*characters*");
    }

    [Fact]
    public void Rehydrate_DoesNotRaiseDomainEvents()
    {
        // #2284 issue 1: read-side bug fix — Rehydrate must not enqueue phantom events.
        // Without this fix, every KnowledgeBaseMappers.ToDomain call silently inserted
        // a fresh VectorDocumentIndexedEvent into the outbox on every database read.
        var doc = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 5,
            indexedAt: DateTime.UtcNow,
            sharedGameId: null);

        doc.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rehydrate_WithMetadata_SetsMetadataWithoutEvent()
    {
        // Replaces the dropped SetMetadata mutator. Metadata is now a single-phase
        // construction concern via Rehydrate (mapper read path).
        var metadata = "{\"quality\": 0.92}";

        var doc = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 5,
            indexedAt: DateTime.UtcNow,
            sharedGameId: null,
            metadata: metadata);

        doc.Metadata.Should().Be(metadata);
        doc.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rehydrate_WithTotalCharacters_SetsValue()
    {
        var doc = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 5,
            indexedAt: DateTime.UtcNow,
            sharedGameId: null,
            totalCharacters: 9876);

        doc.TotalCharacters.Should().Be(9876);
    }

    [Fact]
    public void Rehydrate_WithEmptyLanguage_DefaultsToEn()
    {
        // Lenient defaults on read-path: DB row may have empty/malformed language.
        var doc = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "",
            totalChunks: 5,
            indexedAt: DateTime.UtcNow,
            sharedGameId: null);

        doc.Language.Should().Be("en");
    }

    [Fact]
    public void Rehydrate_WithZeroChunks_DefaultsToOne()
    {
        var doc = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 0,
            indexedAt: DateTime.UtcNow,
            sharedGameId: null);

        doc.TotalChunks.Should().Be(1);
    }

    [Fact]
    public void Rehydrate_WithNegativeTotalCharacters_ClampsToZero()
    {
        var doc = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 5,
            indexedAt: DateTime.UtcNow,
            sharedGameId: null,
            totalCharacters: -42);

        doc.TotalCharacters.Should().Be(0);
    }

    // Helper method — uses Rehydrate for fixture construction (no event side-effects in tests)
    private static VectorDocument CreateTestDocument(
        string language = "en",
        int totalChunks = 10)
    {
        return VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: language,
            totalChunks: totalChunks,
            indexedAt: DateTime.UtcNow,
            sharedGameId: null);
    }
}
