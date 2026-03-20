using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Services;

/// <summary>
/// Tests for the DocumentVersioningService domain service.
/// Issue #3025: Backend 90% Coverage Target - Phase 13
/// </summary>
[Trait("Category", "Unit")]
public sealed class DocumentVersioningServiceTests
{
    private readonly Mock<ISharedGameDocumentRepository> _repositoryMock;
    private readonly DocumentVersioningService _service;

    public DocumentVersioningServiceTests()
    {
        _repositoryMock = new Mock<ISharedGameDocumentRepository>();
        _service = new DocumentVersioningService(_repositoryMock.Object);
    }

    #region SetActiveVersionAsync Tests

    [Fact]
    public async Task SetActiveVersionAsync_DeactivatesOtherVersionsAndActivatesDocument()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var document = new SharedGameDocument(
            documentId,
            sharedGameId,
            pdfDocumentId,
            SharedGameDocumentType.Rulebook,
            "1.0",
            false,
            null,
            DateTime.UtcNow,
            Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.DeactivateOtherVersionsAsync(
                sharedGameId,
                SharedGameDocumentType.Rulebook,
                documentId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetActiveVersionAsync(document);

        // Assert
        _repositoryMock.Verify(
            r => r.DeactivateOtherVersionsAsync(
                sharedGameId,
                SharedGameDocumentType.Rulebook,
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);
        document.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveVersionAsync_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var document = new SharedGameDocument(
            documentId,
            sharedGameId,
            Guid.NewGuid(),
            SharedGameDocumentType.Errata,
            "1.0",
            false,
            null,
            DateTime.UtcNow,
            Guid.NewGuid());

        var cancellationToken = new CancellationToken();

        _repositoryMock
            .Setup(r => r.DeactivateOtherVersionsAsync(
                sharedGameId,
                SharedGameDocumentType.Errata,
                documentId,
                cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetActiveVersionAsync(document, cancellationToken);

        // Assert
        _repositoryMock.Verify(
            r => r.DeactivateOtherVersionsAsync(
                sharedGameId,
                SharedGameDocumentType.Errata,
                documentId,
                cancellationToken),
            Times.Once);
    }

    #endregion

    #region GetVersionHistoryAsync Tests

    [Fact]
    public async Task GetVersionHistoryAsync_ReturnsDocumentsFromRepository()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documents = new List<SharedGameDocument>
        {
            CreateDocument(sharedGameId, SharedGameDocumentType.Rulebook, "1.0", true),
            CreateDocument(sharedGameId, SharedGameDocumentType.Rulebook, "2.0", false)
        };

        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAndTypeAsync(
                sharedGameId,
                SharedGameDocumentType.Rulebook,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        // Act
        var result = await _service.GetVersionHistoryAsync(sharedGameId, SharedGameDocumentType.Rulebook);

        // Assert
        result.Should().HaveCount(2);
        _repositoryMock.Verify(
            r => r.GetBySharedGameIdAndTypeAsync(
                sharedGameId,
                SharedGameDocumentType.Rulebook,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_WithNoDocuments_ReturnsEmptyList()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAndTypeAsync(
                sharedGameId,
                SharedGameDocumentType.Homerule,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SharedGameDocument>());

        // Act
        var result = await _service.GetVersionHistoryAsync(sharedGameId, SharedGameDocumentType.Homerule);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetActiveVersionAsync Tests

    [Fact]
    public async Task GetActiveVersionAsync_ReturnsActiveDocument()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var document = CreateDocument(sharedGameId, SharedGameDocumentType.Rulebook, "1.0", true);

        _repositoryMock
            .Setup(r => r.GetActiveDocumentAsync(
                sharedGameId,
                SharedGameDocumentType.Rulebook,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var result = await _service.GetActiveVersionAsync(sharedGameId, SharedGameDocumentType.Rulebook);

        // Assert
        result.Should().NotBeNull();
        result!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveVersionAsync_WithNoActiveDocument_ReturnsNull()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetActiveDocumentAsync(
                sharedGameId,
                SharedGameDocumentType.Rulebook,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGameDocument?)null);

        // Act
        var result = await _service.GetActiveVersionAsync(sharedGameId, SharedGameDocumentType.Rulebook);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ValidateVersionDoesNotExistAsync Tests

    [Fact]
    public async Task ValidateVersionDoesNotExistAsync_WhenVersionDoesNotExist_DoesNotThrow()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.VersionExistsAsync(
                sharedGameId,
                SharedGameDocumentType.Rulebook,
                "2.0",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var action = async () => await _service.ValidateVersionDoesNotExistAsync(
            sharedGameId,
            SharedGameDocumentType.Rulebook,
            "2.0");

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateVersionDoesNotExistAsync_WhenVersionExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.VersionExistsAsync(
                sharedGameId,
                SharedGameDocumentType.Rulebook,
                "1.0",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var action = async () => await _service.ValidateVersionDoesNotExistAsync(
            sharedGameId,
            SharedGameDocumentType.Rulebook,
            "1.0");

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Version 1.0 already exists for Rulebook documents of game {sharedGameId}*");
    }

    [Fact]
    public async Task ValidateVersionDoesNotExistAsync_ForDifferentDocumentTypes_ValidatesCorrectType()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.VersionExistsAsync(
                sharedGameId,
                SharedGameDocumentType.Errata,
                "1.0",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var action = async () => await _service.ValidateVersionDoesNotExistAsync(
            sharedGameId,
            SharedGameDocumentType.Errata,
            "1.0");

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Errata documents*");
    }

    #endregion

    #region Helper Methods

    private static SharedGameDocument CreateDocument(
        Guid sharedGameId,
        SharedGameDocumentType type,
        string version,
        bool isActive)
    {
        return new SharedGameDocument(
            Guid.NewGuid(),
            sharedGameId,
            Guid.NewGuid(),
            type,
            version,
            isActive,
            null,
            DateTime.UtcNow,
            Guid.NewGuid());
    }

    #endregion
}