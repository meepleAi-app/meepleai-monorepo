using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for UpdateShareRequestDocumentsCommandHandler.
/// Issue #2733: API Endpoints Utente per Share Requests
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UpdateShareRequestDocumentsCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _shareRequestRepositoryMock;
    private readonly Mock<IPdfDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UpdateShareRequestDocumentsCommandHandler>> _loggerMock;
    private readonly UpdateShareRequestDocumentsCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestRequestId = Guid.NewGuid();
    private static readonly Guid TestGameId = Guid.NewGuid();
    private static readonly Guid TestDoc1Id = Guid.NewGuid();
    private static readonly Guid TestDoc2Id = Guid.NewGuid();

    public UpdateShareRequestDocumentsCommandHandlerTests()
    {
        _shareRequestRepositoryMock = new Mock<IShareRequestRepository>();
        _documentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UpdateShareRequestDocumentsCommandHandler>>();

        _handler = new UpdateShareRequestDocumentsCommandHandler(
            _shareRequestRepositoryMock.Object,
            _documentRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesDocuments()
    {
        // Arrange
        var shareRequest = ShareRequest.Create(
            TestUserId,
            TestGameId,
            ContributionType.NewGame,
            userNotes: null,
            targetSharedGameId: null);

        var command = new UpdateShareRequestDocumentsCommand(
            shareRequest.Id,
            TestUserId,
            new List<Guid> { TestDoc1Id, TestDoc2Id });

        var documents = new List<PdfDocument>
        {
            CreateMockDocument(TestDoc1Id, TestUserId),
            CreateMockDocument(TestDoc2Id, TestUserId)
        };

        _shareRequestRepositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        _documentRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _shareRequestRepositoryMock.Verify(
            r => r.Update(It.IsAny<ShareRequest>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        shareRequest.AttachedDocuments.Count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithNonExistentRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new UpdateShareRequestDocumentsCommand(
            TestRequestId,
            TestUserId,
            new List<Guid> { TestDoc1Id });

        _shareRequestRepositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(TestRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WithNonOwner_ThrowsInvalidOperationException()
    {
        // Arrange
        var differentUserId = Guid.NewGuid();
        var shareRequest = ShareRequest.Create(
            differentUserId,
            TestGameId,
            ContributionType.NewGame,
            userNotes: null,
            targetSharedGameId: null);

        var command = new UpdateShareRequestDocumentsCommand(
            shareRequest.Id,
            TestUserId, // Different user
            new List<Guid> { TestDoc1Id });

        _shareRequestRepositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WithDocumentNotOwnedByUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var shareRequest = ShareRequest.Create(
            TestUserId,
            TestGameId,
            ContributionType.NewGame,
            userNotes: null,
            targetSharedGameId: null);

        var command = new UpdateShareRequestDocumentsCommand(
            shareRequest.Id,
            TestUserId,
            new List<Guid> { TestDoc1Id });

        var otherUserId = Guid.NewGuid();
        var documents = new List<PdfDocument>
        {
            CreateMockDocument(TestDoc1Id, otherUserId) // Owned by different user
        };

        _shareRequestRepositoryMock
            .Setup(r => r.GetByIdForUpdateAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        _documentRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static PdfDocument CreateMockDocument(Guid id, Guid userId)
    {
        return new PdfDocument(
            id,
            TestGameId,
            new FileName("test.pdf"),
            "test/path.pdf",
            new FileSize(1024),
            userId);
    }
}