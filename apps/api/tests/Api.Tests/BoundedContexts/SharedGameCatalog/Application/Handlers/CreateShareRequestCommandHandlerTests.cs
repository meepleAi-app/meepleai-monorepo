using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class CreateShareRequestCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _shareRequestRepositoryMock;
    private readonly Mock<ISharedGameRepository> _sharedGameRepositoryMock;
    private readonly Mock<IUserLibraryRepository> _userLibraryRepositoryMock;
    private readonly Mock<IPdfDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<CreateShareRequestCommandHandler>> _loggerMock;
    private readonly CreateShareRequestCommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestGameId = Guid.NewGuid();

    public CreateShareRequestCommandHandlerTests()
    {
        _shareRequestRepositoryMock = new Mock<IShareRequestRepository>();
        _sharedGameRepositoryMock = new Mock<ISharedGameRepository>();
        _userLibraryRepositoryMock = new Mock<IUserLibraryRepository>();
        _documentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<CreateShareRequestCommandHandler>>();

        _handler = new CreateShareRequestCommandHandler(
            _shareRequestRepositoryMock.Object,
            _sharedGameRepositoryMock.Object,
            _userLibraryRepositoryMock.Object,
            _documentRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesNewGameShareRequest()
    {
        // Arrange
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            Notes: "Great game to share!");

        var libraryEntryMock = CreateMockLibraryEntry();
        var sourceGame = CreateMockSharedGame(TestGameId, bggId: null);

        SetupMocks(libraryEntryMock, sourceGame);

        ShareRequest? capturedRequest = null;
        _shareRequestRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ShareRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.NotEqual(Guid.Empty, response.ShareRequestId);
        Assert.Equal(ShareRequestStatus.Pending, response.Status);
        Assert.Equal(ContributionType.NewGame, response.ContributionType);

        _shareRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedRequest);
        Assert.Equal(TestUserId, capturedRequest.UserId);
        Assert.Equal(TestGameId, capturedRequest.SourceGameId);
        Assert.Equal("Great game to share!", capturedRequest.UserNotes);
        Assert.Equal(ContributionType.NewGame, capturedRequest.ContributionType);
    }

    [Fact]
    public async Task Handle_WithExistingBggGame_CreatesAdditionalContentRequest()
    {
        // Arrange
        var existingGameId = Guid.NewGuid();
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId);

        var libraryEntryMock = CreateMockLibraryEntry();
        var sourceGame = CreateMockSharedGame(TestGameId, bggId: 12345);
        var existingPublishedGame = CreateMockSharedGame(existingGameId, bggId: 12345);

        SetupMocks(libraryEntryMock, sourceGame);

        _sharedGameRepositoryMock
            .Setup(r => r.GetByBggIdAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPublishedGame);

        ShareRequest? capturedRequest = null;
        _shareRequestRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ShareRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ContributionType.AdditionalContent, response.ContributionType);
        Assert.NotNull(capturedRequest);
        Assert.Equal(ContributionType.AdditionalContent, capturedRequest.ContributionType);
        Assert.Equal(existingGameId, capturedRequest.TargetSharedGameId);
    }

    [Fact]
    public async Task Handle_WithDocuments_AttachesDocumentsToRequest()
    {
        // Arrange
        var docId1 = Guid.NewGuid();
        var docId2 = Guid.NewGuid();
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId,
            AttachedDocumentIds: [docId1, docId2]);

        var libraryEntryMock = CreateMockLibraryEntry();
        var sourceGame = CreateMockSharedGame(TestGameId, bggId: null);

        SetupMocks(libraryEntryMock, sourceGame);

        var documents = new List<PdfDocument>
        {
            CreateMockPdfDocument(docId1, TestUserId, "rules.pdf", 1024000),
            CreateMockPdfDocument(docId2, TestUserId, "quickstart.pdf", 512000)
        };

        _documentRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        ShareRequest? capturedRequest = null;
        _shareRequestRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ShareRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(2, capturedRequest.AttachedDocuments.Count);
    }

    [Fact]
    public async Task Handle_WithNullLibraryEntry_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId);

        _userLibraryRepositoryMock
            .Setup(r => r.GetByUserAndGameAsync(TestUserId, TestGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _shareRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullSharedGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId);

        var libraryEntryMock = CreateMockLibraryEntry();

        _userLibraryRepositoryMock
            .Setup(r => r.GetByUserAndGameAsync(TestUserId, TestGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntryMock);

        _sharedGameRepositoryMock
            .Setup(r => r.GetByIdAsync(TestGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _shareRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithSameBggIdAsSource_CreatesNewGameRequest()
    {
        // Arrange: When the existing game with same BggId IS the source game itself
        var command = new CreateShareRequestCommand(
            UserId: TestUserId,
            SourceGameId: TestGameId);

        var libraryEntryMock = CreateMockLibraryEntry();
        var sourceGame = CreateMockSharedGame(TestGameId, bggId: 12345);

        SetupMocks(libraryEntryMock, sourceGame);

        // Return the same game (source game) for BggId lookup
        _sharedGameRepositoryMock
            .Setup(r => r.GetByBggIdAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);

        ShareRequest? capturedRequest = null;
        _shareRequestRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ShareRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ShareRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert: Should be NewGame since source and existing are the same
        Assert.Equal(ContributionType.NewGame, response.ContributionType);
        Assert.NotNull(capturedRequest);
        Assert.Null(capturedRequest.TargetSharedGameId);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    private void SetupMocks(UserLibraryEntry libraryEntry, SharedGame sourceGame)
    {
        _userLibraryRepositoryMock
            .Setup(r => r.GetByUserAndGameAsync(TestUserId, TestGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        _sharedGameRepositoryMock
            .Setup(r => r.GetByIdAsync(TestGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);

        _sharedGameRepositoryMock
            .Setup(r => r.GetByBggIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);
    }

    private static UserLibraryEntry CreateMockLibraryEntry()
    {
        // Create UserLibraryEntry using reflection since it's a sealed class
        var entry = (UserLibraryEntry)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(UserLibraryEntry));

        // Use reflection to set the internal fields
        var userIdProperty = typeof(UserLibraryEntry).GetProperty("UserId");
        userIdProperty?.SetValue(entry, TestUserId);

        var gameIdProperty = typeof(UserLibraryEntry).GetProperty("GameId");
        gameIdProperty?.SetValue(entry, TestGameId);

        return entry;
    }

    private static SharedGame CreateMockSharedGame(Guid gameId, int? bggId)
    {
        var game = SharedGame.Create(
            "Test Game",
            2024,
            "Description",
            2,
            4,
            60,
            10,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            null,
            Guid.NewGuid(),
            bggId);

        // Use reflection to set the ID since it's normally set internally
        var idField = typeof(SharedGame).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(game, gameId);

        return game;
    }

    private static PdfDocument CreateMockPdfDocument(Guid docId, Guid userId, string fileName, long fileSize)
    {
        // Create PdfDocument using reflection to set properties
        var doc = (PdfDocument)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PdfDocument));

        // Set the ID field
        var idProperty = typeof(PdfDocument).GetProperty("Id");
        if (idProperty != null && idProperty.CanWrite)
        {
            idProperty.SetValue(doc, docId);
        }

        // Use reflection to set the internal _id field from base class
        var idField = doc.GetType().BaseType?.GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(doc, docId);

        // Set FileName using the property
        var fileNameProperty = typeof(PdfDocument).GetProperty("FileName");
        fileNameProperty?.SetValue(doc, new FileName(fileName));

        // Set ContentType
        var contentTypeProperty = typeof(PdfDocument).GetProperty("ContentType");
        contentTypeProperty?.SetValue(doc, "application/pdf");

        // Set FileSize
        var fileSizeProperty = typeof(PdfDocument).GetProperty("FileSize");
        fileSizeProperty?.SetValue(doc, new FileSize(fileSize));

        // Set UploadedByUserId
        var uploaderProperty = typeof(PdfDocument).GetProperty("UploadedByUserId");
        uploaderProperty?.SetValue(doc, userId);

        return doc;
    }
}
