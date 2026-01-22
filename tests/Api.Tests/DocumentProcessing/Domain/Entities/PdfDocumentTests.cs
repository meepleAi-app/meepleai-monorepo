using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.DocumentProcessing.Domain.Entities;

public sealed class PdfDocumentTests
{
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sharedGameId = Guid.NewGuid();
    private readonly Guid _contributorId = Guid.NewGuid();

    #region CreateCopy Tests

    [Fact]
    public void CreateCopy_WithValidParameters_CreatesNewDocument()
    {
        // Arrange
        var source = CreateTestDocument();
        var newFilePath = "shared-games/test/doc.pdf";

        // Act
        var copy = PdfDocument.CreateCopy(source, _sharedGameId, _contributorId, newFilePath);

        // Assert
        copy.Should().NotBeNull();
        copy.Id.Should().NotBe(source.Id);
        copy.Id.Should().NotBe(Guid.Empty);
        copy.GameId.Should().Be(source.GameId);
        copy.FileName.Value.Should().Be(source.FileName.Value);
        copy.FilePath.Should().Be(newFilePath);
        copy.FileSize.Bytes.Should().Be(source.FileSize.Bytes);
        copy.UploadedByUserId.Should().Be(_contributorId);
        copy.ProcessingStatus.Should().Be(source.ProcessingStatus);
        copy.PageCount.Should().Be(source.PageCount);
        copy.Language.Value.Should().Be(source.Language.Value);
        copy.DocumentType.Value.Should().Be(source.DocumentType.Value);
        copy.IsPublic.Should().BeTrue();
        copy.SharedGameId.Should().Be(_sharedGameId);
        copy.ContributorId.Should().Be(_contributorId);
        copy.SourceDocumentId.Should().Be(source.Id);
        copy.ProcessingError.Should().BeNull();
        copy.CollectionId.Should().BeNull();
        copy.SortOrder.Should().Be(0);
    }

    [Fact]
    public void CreateCopy_WithNullSource_ThrowsArgumentNullException()
    {
        // Act
        var act = () => PdfDocument.CreateCopy(null!, _sharedGameId, _contributorId, "path.pdf");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void CreateCopy_WithEmptySharedGameId_ThrowsArgumentException()
    {
        // Arrange
        var source = CreateTestDocument();

        // Act
        var act = () => PdfDocument.CreateCopy(source, Guid.Empty, _contributorId, "path.pdf");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sharedGameId")
            .WithMessage("*Shared game ID cannot be empty*");
    }

    [Fact]
    public void CreateCopy_WithEmptyContributorId_ThrowsArgumentException()
    {
        // Arrange
        var source = CreateTestDocument();

        // Act
        var act = () => PdfDocument.CreateCopy(source, _sharedGameId, Guid.Empty, "path.pdf");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("contributorId")
            .WithMessage("*Contributor ID cannot be empty*");
    }

    [Fact]
    public void CreateCopy_WithEmptyFilePath_ThrowsArgumentException()
    {
        // Arrange
        var source = CreateTestDocument();

        // Act
        var act = () => PdfDocument.CreateCopy(source, _sharedGameId, _contributorId, "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newFilePath")
            .WithMessage("*New file path cannot be empty*");
    }

    [Fact]
    public void CreateCopy_WithWhitespaceFilePath_ThrowsArgumentException()
    {
        // Arrange
        var source = CreateTestDocument();

        // Act
        var act = () => PdfDocument.CreateCopy(source, _sharedGameId, _contributorId, "   ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newFilePath");
    }

    [Fact]
    public void CreateCopy_PreservesSourceMetadata()
    {
        // Arrange
        var source = new PdfDocument(
            Guid.NewGuid(),
            _gameId,
            new FileName("test.pdf"),
            "original/path.pdf",
            new FileSize(1024000),
            _userId,
            new LanguageCode("it"),
            collectionId: Guid.NewGuid(),
            documentType: DocumentType.Expansion,
            sortOrder: 5);

        source.MarkAsProcessing();
        source.MarkAsCompleted(10);

        // Act
        var copy = PdfDocument.CreateCopy(source, _sharedGameId, _contributorId, "new/path.pdf");

        // Assert
        copy.Language.Value.Should().Be("it");
        copy.DocumentType.Value.Should().Be("expansion");
        copy.PageCount.Should().Be(10);
        copy.ProcessingStatus.Should().Be("completed");
        copy.ProcessedAt.Should().NotBeNull();
        copy.CollectionId.Should().BeNull(); // Reset
        copy.SortOrder.Should().Be(0); // Reset
    }

    #endregion

    private PdfDocument CreateTestDocument()
    {
        var document = new PdfDocument(
            Guid.NewGuid(),
            _gameId,
            new FileName("rulebook.pdf"),
            "uploads/rulebook.pdf",
            new FileSize(2048000),
            _userId);

        document.MarkAsCompleted(15);
        return document;
    }
}
