using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure.Services;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.DocumentProcessing.Infrastructure.Services;

public sealed class ShareRequestDocumentServiceTests
{
    private readonly Mock<IPdfDocumentRepository> _documentRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<IStorageService> _storageService;
    private readonly Mock<ILogger<ShareRequestDocumentService>> _logger;
    private readonly ShareRequestDocumentService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();
    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _sharedGameId = Guid.NewGuid();

    public ShareRequestDocumentServiceTests()
    {
        _documentRepo = new Mock<IPdfDocumentRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _storageService = new Mock<IStorageService>();
        _logger = new Mock<ILogger<ShareRequestDocumentService>>();

        _sut = new ShareRequestDocumentService(
            _documentRepo.Object,
            _shareRequestRepo.Object,
            _storageService.Object,
            _logger.Object);
    }

    #region ValidateDocumentOwnership Tests

    [Fact]
    public async Task ValidateDocumentOwnership_WhenUserOwnsDocument_ReturnsTrue()
    {
        // Arrange
        var document = CreateTestDocument(_userId);
        _documentRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var result = await _sut.ValidateDocumentOwnership(_userId, _documentId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateDocumentOwnership_WhenUserDoesNotOwnDocument_ReturnsFalse()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var document = CreateTestDocument(otherUserId);
        _documentRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var result = await _sut.ValidateDocumentOwnership(_userId, _documentId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDocumentOwnership_WhenDocumentNotFound_ReturnsFalse()
    {
        // Arrange
        _documentRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        // Act
        var result = await _sut.ValidateDocumentOwnership(_userId, _documentId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CopyDocumentsToSharedGame Tests

    [Fact]
    public async Task CopyDocumentsToSharedGame_WithValidDocuments_CopiesSuccessfully()
    {
        // Arrange
        var doc1Id = Guid.NewGuid();
        var doc2Id = Guid.NewGuid();
        var documentIds = new List<Guid> { doc1Id, doc2Id };
        var documents = new List<PdfDocument>
        {
            CreateTestDocument(_userId, doc1Id),
            CreateTestDocument(_userId, doc2Id)
        };

        _documentRepo.Setup(r => r.GetByIdsAsync(documentIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        _storageService.Setup(s => s.CopyFile(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((string source, string dest, CancellationToken ct) => dest);

        // Act
        var result = await _sut.CopyDocumentsToSharedGame(
            documentIds,
            _sharedGameId,
            _userId);

        // Assert
        result.Should().HaveCount(2);
        _documentRepo.Verify(r => r.AddAsync(
            It.Is<PdfDocument>(d =>
                d.SharedGameId == _sharedGameId &&
                d.ContributorId == _userId &&
                d.SourceDocumentId != null),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CopyDocumentsToSharedGame_WithEmptyList_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.CopyDocumentsToSharedGame(
            new List<Guid>(),
            _sharedGameId,
            _userId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetDocumentPreview Tests

    [Fact]
    public async Task GetDocumentPreview_WithValidDocument_ReturnsPreview()
    {
        // Arrange
        var document = CreateTestDocument(_userId);
        var previewUrl = "https://example.com/preview";

        _documentRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _storageService.Setup(s => s.GetPreviewUrl(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(previewUrl);

        // Act
        var result = await _sut.GetDocumentPreview(_documentId);

        // Assert
        result.Should().NotBeNull();
        result.DocumentId.Should().Be(document.Id);
        result.FileName.Should().Be(document.FileName.Value);
        result.PreviewUrl.Should().Be(previewUrl);
    }

    [Fact]
    public async Task GetDocumentPreview_WhenDocumentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _documentRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        // Act
        var act = async () => await _sut.GetDocumentPreview(_documentId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    private PdfDocument CreateTestDocument(Guid? uploadedBy = null, Guid? id = null)
    {
        var document = new PdfDocument(
            id ?? Guid.NewGuid(),
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "uploads/test.pdf",
            new FileSize(1024000),
            uploadedBy ?? _userId);

        document.MarkAsCompleted(10);
        return document;
    }
}
