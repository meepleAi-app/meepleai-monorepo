using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Unit tests for VectorDocument aggregate root.
/// Issue #2639: KnowledgeBase test suite
/// Issue #2244: factory + rehydrate pattern (epic #2242 Sub #2)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class VectorDocumentTests
{
    // ── #2244 new factory tests ───────────────────────────────────────────────

    [Fact]
    public void Create_WithValidArgs_RaisesVectorDocumentIndexedEventOnce()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        // Act
        var doc = VectorDocument.Create(
            id: id,
            gameId: gameId,
            pdfDocumentId: pdfId,
            language: "en",
            totalChunks: 42,
            sharedGameId: sharedGameId);

        // Assert
        doc.DomainEvents.OfType<VectorDocumentIndexedEvent>().Should().HaveCount(1);
        var evt = doc.DomainEvents.OfType<VectorDocumentIndexedEvent>().Single();
        evt.DocumentId.Should().Be(id);
        evt.GameId.Should().Be(gameId);
        evt.ChunkCount.Should().Be(42);
        evt.SharedGameId.Should().Be(sharedGameId);
    }

    [Fact]
    public void Create_NormalizesLanguageToLowerInvariant()
    {
        var doc = VectorDocument.Create(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "EN-US",
            totalChunks: 1);

        doc.Language.Should().Be("en-us");
    }

    [Fact]
    public void Create_WithEmptyLanguage_Throws()
    {
        var act = () => VectorDocument.Create(
            id: Guid.NewGuid(), gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(), language: "",
            totalChunks: 1);

        act.Should().Throw<ArgumentException>().WithMessage("*language*");
    }

    [Fact]
    public void Create_WithZeroChunks_Throws()
    {
        var act = () => VectorDocument.Create(
            id: Guid.NewGuid(), gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(), language: "en",
            totalChunks: 0);

        act.Should().Throw<ArgumentException>().WithMessage("*chunks*");
    }

    [Fact]
    public void Rehydrate_DoesNotRaiseDomainEvents()
    {
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
    public void PublicConstructor_IsNotAccessible()
    {
        // Sub #2 guard: prevent regression to old anti-pattern (3 ingestion paths used `new VectorDocument(...)`)
        var ctor = typeof(VectorDocument)
            .GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        ctor.Should().BeEmpty("VectorDocument must only be constructible via Create() factory or Rehydrate() (read-side)");
    }

    // ── Existing tests migrated to factory/rehydrate (#2244) ─────────────────

    [Fact]
    public void Create_ValidParameters_CreatesVectorDocument()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var language = "en";
        var totalChunks = 10;

        // Act
        var document = VectorDocument.Create(id, gameId, pdfDocumentId, language, totalChunks);

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
    public void Create_LanguageWithUpperCase_NormalizesToLowerCase()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act
        var document = VectorDocument.Create(id, gameId, pdfDocumentId, "EN-US", 5);

        // Assert
        document.Language.Should().Be("en-us");
    }

    [Fact]
    public void Create_EmptyLanguage_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        Action act = () =>
            VectorDocument.Create(id, gameId, pdfDocumentId, "", 10);
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().Contain("Language cannot be empty");
    }

    [Fact]
    public void Create_WhitespaceLanguage_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        Action act = () =>
            VectorDocument.Create(id, gameId, pdfDocumentId, "   ", 10);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ZeroChunks_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        Action act = () =>
            VectorDocument.Create(id, gameId, pdfDocumentId, "en", 0);
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().Contain("Total chunks must be positive");
    }

    [Fact]
    public void Create_NegativeChunks_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();

        // Act & Assert
        Action act = () =>
            VectorDocument.Create(id, gameId, pdfDocumentId, "en", -5);
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
    public void SetMetadata_NullValue_SetsToNull()
    {
        // Arrange
        var document = CreateTestDocument();
        document.UpdateMetadata("{\"test\": true}");

        // Act
        document.SetMetadata(null);

        // Assert
        document.Metadata.Should().BeNull();
    }

    [Fact]
    public void Create_SetsIndexedAtToCurrentTime()
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
    public void Create_WithoutSharedGameId_DefaultsToNull()
    {
        var document = CreateTestDocument();

        document.SharedGameId.Should().BeNull();
    }

    [Fact]
    public void Create_WithSharedGameId_SetsSharedGameId()
    {
        var sharedGameId = Guid.NewGuid();

        var document = VectorDocument.Create(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 10,
            sharedGameId: sharedGameId);

        document.SharedGameId.Should().Be(sharedGameId);
    }

    [Fact]
    public void SetSharedGameId_UpdatesValue()
    {
        var document = CreateTestDocument();
        var sharedGameId = Guid.NewGuid();

        document.SetSharedGameId(sharedGameId);

        document.SharedGameId.Should().Be(sharedGameId);
    }

    [Fact]
    public void SetSharedGameId_WithNull_ClearsValue()
    {
        var sharedGameId = Guid.NewGuid();
        var document = VectorDocument.Create(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 5,
            sharedGameId: sharedGameId);

        document.SetSharedGameId(null);

        document.SharedGameId.Should().BeNull();
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
