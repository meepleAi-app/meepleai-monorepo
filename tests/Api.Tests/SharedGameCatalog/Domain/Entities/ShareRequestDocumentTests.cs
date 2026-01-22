using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Domain.Entities;

public sealed class ShareRequestDocumentTests
{
    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_CreatesDocument()
    {
        // Act
        var document = ShareRequestDocument.Create(
            _shareRequestId,
            _documentId,
            "rules.pdf",
            "application/pdf",
            1024);

        // Assert
        document.Should().NotBeNull();
        document.Id.Should().NotBe(Guid.Empty);
        document.ShareRequestId.Should().Be(_shareRequestId);
        document.DocumentId.Should().Be(_documentId);
        document.FileName.Should().Be("rules.pdf");
        document.ContentType.Should().Be("application/pdf");
        document.FileSize.Should().Be(1024);
        document.AttachedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithEmptyShareRequestId_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => ShareRequestDocument.Create(
            Guid.Empty,
            _documentId,
            "test.pdf",
            "application/pdf",
            1024);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("shareRequestId");
    }

    [Fact]
    public void Create_WithEmptyDocumentId_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => ShareRequestDocument.Create(
            _shareRequestId,
            Guid.Empty,
            "test.pdf",
            "application/pdf",
            1024);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidFileName_ThrowsArgumentException(string? invalidName)
    {
        // Act & Assert
        var action = () => ShareRequestDocument.Create(
            _shareRequestId,
            _documentId,
            invalidName!,
            "application/pdf",
            1024);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("fileName");
    }

    [Fact]
    public void Create_WithTooLongFileName_ThrowsArgumentException()
    {
        // Arrange
        var longFileName = new string('x', 256);

        // Act & Assert
        var action = () => ShareRequestDocument.Create(
            _shareRequestId,
            _documentId,
            longFileName,
            "application/pdf",
            1024);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("fileName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidContentType_ThrowsArgumentException(string? invalidType)
    {
        // Act & Assert
        var action = () => ShareRequestDocument.Create(
            _shareRequestId,
            _documentId,
            "test.pdf",
            invalidType!,
            1024);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("contentType");
    }

    [Fact]
    public void Create_WithTooLongContentType_ThrowsArgumentException()
    {
        // Arrange
        var longContentType = new string('x', 101);

        // Act & Assert
        var action = () => ShareRequestDocument.Create(
            _shareRequestId,
            _documentId,
            "test.pdf",
            longContentType,
            1024);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("contentType");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithInvalidFileSize_ThrowsArgumentException(long invalidSize)
    {
        // Act & Assert
        var action = () => ShareRequestDocument.Create(
            _shareRequestId,
            _documentId,
            "test.pdf",
            "application/pdf",
            invalidSize);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("fileSize");
    }

    [Fact]
    public void Create_TrimsFileNameAndContentType()
    {
        // Act
        var document = ShareRequestDocument.Create(
            _shareRequestId,
            _documentId,
            "  rules.pdf  ",
            "  application/pdf  ",
            1024);

        // Assert
        document.FileName.Should().Be("rules.pdf");
        document.ContentType.Should().Be("application/pdf");
    }
}
