using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Security tests verifying that collection modification commands enforce
/// authorization correctly — returning 403 Forbidden (not 400 Bad Request)
/// when a user attempts to modify a collection they do not own.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class CollectionAuthorizationCommandHandlerTests
{
    private readonly Mock<IDocumentCollectionRepository> _mockCollectionRepository;
    private readonly Mock<IPdfDocumentRepository> _mockPdfRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;

    public CollectionAuthorizationCommandHandlerTests()
    {
        _mockCollectionRepository = new Mock<IDocumentCollectionRepository>();
        _mockPdfRepository = new Mock<IPdfDocumentRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
    }

    // ──────────────────────────────────────────────
    // AddDocumentToCollection
    // ──────────────────────────────────────────────

    [Fact]
    public async Task AddDocument_WhenUserDoesNotOwnCollection_ThrowsForbiddenException()
    {
        // Arrange
        var collectionOwner = Guid.NewGuid();
        var attackerUserId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var collection = new DocumentCollection(
            id: Guid.NewGuid(),
            gameId: gameId,
            name: new CollectionName("Rulebooks"),
            createdByUserId: collectionOwner);

        _mockCollectionRepository
            .Setup(r => r.GetByIdAsync(collection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);

        var handler = new AddDocumentToCollectionCommandHandler(
            _mockCollectionRepository.Object,
            _mockPdfRepository.Object,
            _mockUnitOfWork.Object,
            Mock.Of<ILogger<AddDocumentToCollectionCommandHandler>>());

        var command = new AddDocumentToCollectionCommand(
            CollectionId: collection.Id,
            PdfDocumentId: Guid.NewGuid(),
            UserId: attackerUserId,
            DocumentType: "rulebook",
            SortOrder: 1);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>(
            because: "an authorization failure must return 403 Forbidden, not 400 Bad Request");
    }

    [Fact]
    public async Task AddDocument_WhenUserDoesNotOwnCollection_ErrorMessageShouldNotContainIds()
    {
        // Arrange — ensure the message leaks neither the user ID nor the collection ID
        var collectionOwner = Guid.NewGuid();
        var attackerUserId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var collection = new DocumentCollection(
            id: Guid.NewGuid(),
            gameId: gameId,
            name: new CollectionName("Rulebooks"),
            createdByUserId: collectionOwner);

        _mockCollectionRepository
            .Setup(r => r.GetByIdAsync(collection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);

        var handler = new AddDocumentToCollectionCommandHandler(
            _mockCollectionRepository.Object,
            _mockPdfRepository.Object,
            _mockUnitOfWork.Object,
            Mock.Of<ILogger<AddDocumentToCollectionCommandHandler>>());

        var command = new AddDocumentToCollectionCommand(
            CollectionId: collection.Id,
            PdfDocumentId: Guid.NewGuid(),
            UserId: attackerUserId,
            DocumentType: "rulebook",
            SortOrder: 1);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<ForbiddenException>()).Which;
        exception.Message.Should().NotContain(attackerUserId.ToString(),
            because: "user ID must not appear in error messages (prevents IDOR reconnaissance)");
        exception.Message.Should().NotContain(collection.Id.ToString(),
            because: "collection ID must not appear in error messages (prevents IDOR reconnaissance)");
    }

    // ──────────────────────────────────────────────
    // RemoveDocumentFromCollection
    // ──────────────────────────────────────────────

    [Fact]
    public async Task RemoveDocument_WhenUserDoesNotOwnCollection_ThrowsForbiddenException()
    {
        // Arrange
        var collectionOwner = Guid.NewGuid();
        var attackerUserId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var collection = new DocumentCollection(
            id: Guid.NewGuid(),
            gameId: gameId,
            name: new CollectionName("Rulebooks"),
            createdByUserId: collectionOwner);

        _mockCollectionRepository
            .Setup(r => r.GetByIdAsync(collection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);

        var handler = new RemoveDocumentFromCollectionCommandHandler(
            _mockCollectionRepository.Object,
            _mockUnitOfWork.Object,
            Mock.Of<ILogger<RemoveDocumentFromCollectionCommandHandler>>());

        var command = new RemoveDocumentFromCollectionCommand(
            CollectionId: collection.Id,
            PdfDocumentId: Guid.NewGuid(),
            UserId: attackerUserId);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>(
            because: "an authorization failure must return 403 Forbidden, not 400 Bad Request");
    }

    [Fact]
    public async Task RemoveDocument_WhenUserDoesNotOwnCollection_ErrorMessageShouldNotContainIds()
    {
        // Arrange
        var collectionOwner = Guid.NewGuid();
        var attackerUserId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var collection = new DocumentCollection(
            id: Guid.NewGuid(),
            gameId: gameId,
            name: new CollectionName("Rulebooks"),
            createdByUserId: collectionOwner);

        _mockCollectionRepository
            .Setup(r => r.GetByIdAsync(collection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collection);

        var handler = new RemoveDocumentFromCollectionCommandHandler(
            _mockCollectionRepository.Object,
            _mockUnitOfWork.Object,
            Mock.Of<ILogger<RemoveDocumentFromCollectionCommandHandler>>());

        var command = new RemoveDocumentFromCollectionCommand(
            CollectionId: collection.Id,
            PdfDocumentId: Guid.NewGuid(),
            UserId: attackerUserId);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<ForbiddenException>()).Which;
        exception.Message.Should().NotContain(attackerUserId.ToString(),
            because: "user ID must not appear in error messages (prevents IDOR reconnaissance)");
        exception.Message.Should().NotContain(collection.Id.ToString(),
            because: "collection ID must not appear in error messages (prevents IDOR reconnaissance)");
    }
}
