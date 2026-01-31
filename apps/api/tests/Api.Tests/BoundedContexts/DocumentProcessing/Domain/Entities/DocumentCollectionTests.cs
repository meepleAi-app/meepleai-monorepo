using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Unit tests for DocumentCollection aggregate root.
/// Issue #2051: Multi-document collection business logic
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DocumentCollectionTests
{
    private static Guid GameId => new("12345678-1234-1234-1234-123456789012");
    private static Guid UserId => new("87654321-4321-4321-4321-210987654321");
    private static Guid PdfId1 => new("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    private static Guid PdfId2 => new("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

    [Fact]
    public void Constructor_ValidArguments_CreatesCollection()
    {
        // Arrange
        var collectionName = new CollectionName("Test Collection");

        // Act
        var collection = new DocumentCollection(
            Guid.NewGuid(),
            GameId,
            collectionName,
            UserId,
            "Test description");

        // Assert
        collection.Id.Should().NotBeEmpty();
        collection.GameId.Should().Be(GameId);
        collection.Name.Should().Be(collectionName);
        collection.CreatedByUserId.Should().Be(UserId);
        collection.Description.Should().Be("Test description");
        collection.Documents.Should().BeEmpty();
        collection.IsEmpty.Should().BeTrue();
        collection.IsFull.Should().BeFalse();
    }

    [Fact]
    public void Constructor_EmptyGameId_ThrowsArgumentException()
    {
        // Arrange
        var collectionName = new CollectionName("Test");

        // Act
        Action act = () => new DocumentCollection(Guid.NewGuid(), Guid.Empty, collectionName, UserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Game ID cannot be empty*");
    }

    [Fact]
    public void Constructor_EmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var collectionName = new CollectionName("Test");

        // Act
        Action act = () => new DocumentCollection(Guid.NewGuid(), GameId, collectionName, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Created by user ID cannot be empty*");
    }

    [Fact]
    public void AddDocument_ValidDocument_AddsSuccessfully()
    {
        // Arrange
        var collection = CreateTestCollection();

        // Act
        collection.AddDocument(PdfId1, DocumentType.Base, 0);

        // Assert
        collection.Documents.Should().HaveCount(1);
        collection.Documents[0].PdfDocumentId.Should().Be(PdfId1);
        collection.Documents[0].Type.Should().Be(DocumentType.Base);
        collection.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void AddDocument_EmptyPdfId_ThrowsArgumentException()
    {
        // Arrange
        var collection = CreateTestCollection();

        // Act
        Action act = () => collection.AddDocument(Guid.Empty, DocumentType.Base, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*PDF document ID cannot be empty*");
    }

    [Fact]
    public void AddDocument_NullDocumentType_ThrowsArgumentNullException()
    {
        // Arrange
        var collection = CreateTestCollection();

        // Act
        Action act = () => collection.AddDocument(PdfId1, null!, 0);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddDocument_MaxDocumentsReached_ThrowsDomainException()
    {
        // Arrange
        var collection = CreateTestCollection();
        for (int i = 0; i < 5; i++)
        {
            collection.AddDocument(Guid.NewGuid(), DocumentType.Base, i);
        }

        // Act
        Action act = () => collection.AddDocument(Guid.NewGuid(), DocumentType.Base, 5);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*already contains maximum of 5 documents*");
        collection.IsFull.Should().BeTrue();
    }

    [Fact]
    public void AddDocument_DuplicateDocument_ThrowsDomainException()
    {
        // Arrange
        var collection = CreateTestCollection();
        collection.AddDocument(PdfId1, DocumentType.Base, 0);

        // Act
        Action act = () => collection.AddDocument(PdfId1, DocumentType.Expansion, 1);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*already in this collection*");
    }

    [Fact]
    public void RemoveDocument_ExistingDocument_RemovesSuccessfully()
    {
        // Arrange
        var collection = CreateTestCollection();
        collection.AddDocument(PdfId1, DocumentType.Base, 0);

        // Act
        collection.RemoveDocument(PdfId1);

        // Assert
        collection.Documents.Should().BeEmpty();
        collection.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void RemoveDocument_NonExistentDocument_ThrowsDomainException()
    {
        // Arrange
        var collection = CreateTestCollection();

        // Act
        Action act = () => collection.RemoveDocument(PdfId1);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*not found in collection*");
    }

    [Fact]
    public void GetDocument_ExistingDocument_ReturnsDocument()
    {
        // Arrange
        var collection = CreateTestCollection();
        collection.AddDocument(PdfId1, DocumentType.Base, 0);

        // Act
        var doc = collection.GetDocument(PdfId1);

        // Assert
        doc.Should().NotBeNull();
        doc!.PdfDocumentId.Should().Be(PdfId1);
    }

    [Fact]
    public void GetDocument_NonExistentDocument_ReturnsNull()
    {
        // Arrange
        var collection = CreateTestCollection();

        // Act
        var doc = collection.GetDocument(PdfId1);

        // Assert
        doc.Should().BeNull();
    }

    [Fact]
    public void GetDocumentsOrdered_ReturnsDocumentsBySortOrder()
    {
        // Arrange
        var collection = CreateTestCollection();
        collection.AddDocument(PdfId2, DocumentType.Expansion, 2);
        collection.AddDocument(PdfId1, DocumentType.Base, 0);

        // Act
        var ordered = collection.GetDocumentsOrdered();

        // Assert
        ordered.Should().HaveCount(2);
        ordered[0].PdfDocumentId.Should().Be(PdfId1); // SortOrder 0
        ordered[1].PdfDocumentId.Should().Be(PdfId2); // SortOrder 2
    }

    [Fact]
    public void ContainsDocumentType_ExistingType_ReturnsTrue()
    {
        // Arrange
        var collection = CreateTestCollection();
        collection.AddDocument(PdfId1, DocumentType.Base, 0);

        // Act & Assert
        collection.ContainsDocumentType(DocumentType.Base).Should().BeTrue();
    }

    [Fact]
    public void ContainsDocumentType_NonExistentType_ReturnsFalse()
    {
        // Arrange
        var collection = CreateTestCollection();
        collection.AddDocument(PdfId1, DocumentType.Base, 0);

        // Act & Assert
        collection.ContainsDocumentType(DocumentType.Homerule).Should().BeFalse();
    }

    [Fact]
    public void DocumentCount_ReturnsCorrectCount()
    {
        // Arrange
        var collection = CreateTestCollection();
        collection.AddDocument(PdfId1, DocumentType.Base, 0);
        collection.AddDocument(PdfId2, DocumentType.Expansion, 1);

        // Act & Assert
        collection.DocumentCount.Should().Be(2);
    }

    [Fact]
    public async Task UpdateMetadata_ValidInput_UpdatesSuccessfully()
    {
        // Arrange
        var collection = CreateTestCollection();
        var originalUpdatedAt = collection.UpdatedAt;
        var newName = new CollectionName("New Collection Name");

        // Simulate time passing
        await Task.Delay(TestConstants.Timing.TinyDelay);

        // Act
        collection.UpdateMetadata(newName, "New description");

        // Assert
        collection.Name.Should().Be(newName);
        collection.Description.Should().Be("New description");
        collection.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UpdateMetadata_NullName_ThrowsArgumentNullException()
    {
        // Arrange
        var collection = CreateTestCollection();

        // Act
        Action act = () => collection.UpdateMetadata(null!, "desc");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static DocumentCollection CreateTestCollection()
    {
        return new DocumentCollection(
            Guid.NewGuid(),
            GameId,
            new CollectionName("Test Collection"),
            UserId,
            "Test description");
    }
}
