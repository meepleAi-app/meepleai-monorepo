using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the ShareRequestDocument entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 13
/// </summary>
[Trait("Category", "Unit")]
public sealed class ShareRequestDocumentTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_ReturnsShareRequestDocument()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        // Act
        var document = ShareRequestDocument.Create(
            shareRequestId,
            documentId,
            "rulebook.pdf",
            "application/pdf",
            1024 * 1024);

        // Assert
        document.Id.Should().NotBe(Guid.Empty);
        document.ShareRequestId.Should().Be(shareRequestId);
        document.DocumentId.Should().Be(documentId);
        document.FileName.Should().Be("rulebook.pdf");
        document.ContentType.Should().Be("application/pdf");
        document.FileSize.Should().Be(1024 * 1024);
        document.AttachedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_TrimsFileName()
    {
        // Act
        var document = ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  rulebook.pdf  ",
            "application/pdf",
            1024);

        // Assert
        document.FileName.Should().Be("rulebook.pdf");
    }

    [Fact]
    public void Create_TrimsContentType()
    {
        // Act
        var document = ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "rulebook.pdf",
            "  application/pdf  ",
            1024);

        // Assert
        document.ContentType.Should().Be("application/pdf");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Create_WithEmptyShareRequestId_ThrowsArgumentException()
    {
        // Act
        var action = () => ShareRequestDocument.Create(
            Guid.Empty,
            Guid.NewGuid(),
            "rulebook.pdf",
            "application/pdf",
            1024);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ShareRequestId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyDocumentId_ThrowsArgumentException()
    {
        // Act
        var action = () => ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.Empty,
            "rulebook.pdf",
            "application/pdf",
            1024);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*DocumentId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyFileName_ThrowsArgumentException(string? fileName)
    {
        // Act
        var action = () => ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            fileName!,
            "application/pdf",
            1024);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*FileName is required*");
    }

    [Fact]
    public void Create_WithFileNameExceeding255Characters_ThrowsArgumentException()
    {
        // Arrange
        var longFileName = new string('a', 252) + ".pdf"; // 256 chars total

        // Act
        var action = () => ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            longFileName,
            "application/pdf",
            1024);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*FileName cannot exceed 255 characters*");
    }

    [Fact]
    public void Create_WithFileNameAt255Characters_Succeeds()
    {
        // Arrange
        var fileName = new string('a', 251) + ".pdf"; // 255 chars total

        // Act
        var document = ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            fileName,
            "application/pdf",
            1024);

        // Assert
        document.FileName.Should().HaveLength(255);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyContentType_ThrowsArgumentException(string? contentType)
    {
        // Act
        var action = () => ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "rulebook.pdf",
            contentType!,
            1024);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ContentType is required*");
    }

    [Fact]
    public void Create_WithContentTypeExceeding100Characters_ThrowsArgumentException()
    {
        // Arrange
        var longContentType = new string('a', 101);

        // Act
        var action = () => ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "rulebook.pdf",
            longContentType,
            1024);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ContentType cannot exceed 100 characters*");
    }

    [Fact]
    public void Create_WithContentTypeAt100Characters_Succeeds()
    {
        // Arrange
        var contentType = new string('a', 100);

        // Act
        var document = ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "rulebook.pdf",
            contentType,
            1024);

        // Assert
        document.ContentType.Should().HaveLength(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Create_WithInvalidFileSize_ThrowsArgumentException(long fileSize)
    {
        // Act
        var action = () => ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "rulebook.pdf",
            "application/pdf",
            fileSize);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*FileSize must be greater than zero*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    [InlineData(long.MaxValue)]
    public void Create_WithValidFileSize_Succeeds(long fileSize)
    {
        // Act
        var document = ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "rulebook.pdf",
            "application/pdf",
            fileSize);

        // Assert
        document.FileSize.Should().Be(fileSize);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Properties_ReturnCorrectValues()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        // Act
        var document = ShareRequestDocument.Create(
            shareRequestId,
            documentId,
            "game_rules.pdf",
            "application/pdf",
            2048);

        // Assert
        document.ShareRequestId.Should().Be(shareRequestId);
        document.DocumentId.Should().Be(documentId);
        document.FileName.Should().Be("game_rules.pdf");
        document.ContentType.Should().Be("application/pdf");
        document.FileSize.Should().Be(2048);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Act
        var doc1 = ShareRequestDocument.Create(
            Guid.NewGuid(), Guid.NewGuid(), "file1.pdf", "application/pdf", 1024);
        var doc2 = ShareRequestDocument.Create(
            Guid.NewGuid(), Guid.NewGuid(), "file2.pdf", "application/pdf", 1024);

        // Assert
        doc1.Id.Should().NotBe(doc2.Id);
    }

    #endregion

    #region Common Content Types Tests

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("application/msword")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void Create_AcceptsCommonContentTypes(string contentType)
    {
        // Act
        var document = ShareRequestDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "file.doc",
            contentType,
            1024);

        // Assert
        document.ContentType.Should().Be(contentType);
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_CreatesDocumentWithAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var shareRequestId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var attachedAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var document = new ShareRequestDocument(
            id,
            shareRequestId,
            documentId,
            "existing.pdf",
            "application/pdf",
            5000,
            attachedAt);

        // Assert
        document.Id.Should().Be(id);
        document.ShareRequestId.Should().Be(shareRequestId);
        document.DocumentId.Should().Be(documentId);
        document.FileName.Should().Be("existing.pdf");
        document.ContentType.Should().Be("application/pdf");
        document.FileSize.Should().Be(5000);
        document.AttachedAt.Should().Be(attachedAt);
    }

    #endregion
}
