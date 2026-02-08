using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveGameProposalCommandHandlerTests
{
    private readonly Mock<IShareRequestRepository> _shareRequestRepoMock;
    private readonly Mock<IPrivateGameRepository> _privateGameRepoMock;
    private readonly Mock<ISharedGameRepository> _sharedGameRepoMock;
    private readonly Mock<IShareRequestDocumentService> _documentServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ApproveGameProposalCommandHandler>> _loggerMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ApproveGameProposalCommandHandler _handler;

    private static readonly Guid TestAdminId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestPrivateGameId = Guid.NewGuid();
    private static readonly Guid TestLibraryEntryId = Guid.NewGuid();

    public ApproveGameProposalCommandHandlerTests()
    {
        _shareRequestRepoMock = new Mock<IShareRequestRepository>();
        _privateGameRepoMock = new Mock<IPrivateGameRepository>();
        _sharedGameRepoMock = new Mock<ISharedGameRepository>();
        _documentServiceMock = new Mock<IShareRequestDocumentService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ApproveGameProposalCommandHandler>>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext($"ApproveGameProposalTest_{Guid.NewGuid()}");

        _handler = new ApproveGameProposalCommandHandler(
            _shareRequestRepoMock.Object,
            _privateGameRepoMock.Object,
            _sharedGameRepoMock.Object,
            _documentServiceMock.Object,
            _dbContext,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Constructor_CreatesCommandWithAllProperties()
    {
        // Arrange & Act
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: "Test notes");

        // Assert
        Assert.NotEqual(Guid.Empty, command.ShareRequestId);
        Assert.Equal(TestAdminId, command.AdminId);
        Assert.Equal(ProposalApprovalAction.ApproveAsNew, command.ApprovalAction);
        Assert.Null(command.TargetSharedGameId);
        Assert.Equal("Test notes", command.AdminNotes);
    }

    [Fact]
    public async Task Handle_WithNonExistentShareRequest_ThrowsNotFoundException()
    {
        // Arrange
        var command = new ApproveGameProposalCommand(
            ShareRequestId: Guid.NewGuid(),
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: null);

        _shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithNonProposalShareRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var shareRequest = ShareRequest.Create(
            TestUserId,
            TestPrivateGameId,
            ContributionType.NewGame, // Not a NewGameProposal
            "Test notes");
        shareRequest.StartReview(TestAdminId);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: null);

        _shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act & Assert - Handler throws ConflictException (409) for non-proposal requests
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithNonExistentLibraryEntry_ThrowsNotFoundException()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview();
        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: null);

        _shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // No library entry seeded in DbContext

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithLibraryEntryWithoutPrivateGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview();

        // Seed library entry WITHOUT PrivateGameId
        var libraryEntry = new UserLibraryEntryEntity
        {
            Id = shareRequest.SourceGameId,
            UserId = TestUserId,
            PrivateGameId = null, // Missing PrivateGameId
            AddedAt = DateTime.UtcNow,
                    };
        _dbContext.UserLibraryEntries.Add(libraryEntry);
        await _dbContext.SaveChangesAsync();

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: null);

        _shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act & Assert - Handler throws ConflictException (409) for missing PrivateGameId
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithNonExistentPrivateGame_ThrowsNotFoundException()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview();

        // Seed library entry WITH valid PrivateGameId
        var libraryEntry = new UserLibraryEntryEntity
        {
            Id = shareRequest.SourceGameId,
            UserId = TestUserId,
            PrivateGameId = TestPrivateGameId,
            AddedAt = DateTime.UtcNow,
                    };
        _dbContext.UserLibraryEntries.Add(libraryEntry);
        await _dbContext.SaveChangesAsync();

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: null);

        _shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(TestPrivateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithApproveAsNew_CreatesNewSharedGameAndApprovesRequest()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview();
        var privateGame = CreatePrivateGame();

        await SeedLibraryEntryAsync(shareRequest.SourceGameId);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: "Looks great!");

        _shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(TestPrivateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        SharedGame? capturedSharedGame = null;
        _sharedGameRepoMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((g, _) => capturedSharedGame = g)
            .Returns(Task.CompletedTask);

        ShareRequest? capturedRequest = null;
        _shareRequestRepoMock
            .Setup(r => r.Update(It.IsAny<ShareRequest>()))
            .Callback<ShareRequest>(r => capturedRequest = r);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(shareRequest.Id, response.ShareRequestId);
        Assert.Equal(ShareRequestStatus.Approved, response.Status);

        // Verify new SharedGame was created
        _sharedGameRepoMock.Verify(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(capturedSharedGame);
        Assert.Equal(privateGame.Title, capturedSharedGame.Title);
        Assert.Equal(privateGame.BggId, capturedSharedGame.BggId);

        // Verify ShareRequest was approved
        _shareRequestRepoMock.Verify(r => r.Update(It.IsAny<ShareRequest>()), Times.Once);
        Assert.NotNull(capturedRequest);
        Assert.Equal(ShareRequestStatus.Approved, capturedRequest.Status);
        Assert.Equal(capturedSharedGame.Id, capturedRequest.TargetSharedGameId);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMergeKnowledgeBase_DoesNotCreateNewGame()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview();
        var privateGame = CreatePrivateGame();
        var existingGame = SharedGame.Create("Existing Game", 2020, "An existing game", 2, 4, 60, 10, 2.5m, null, "https://example.com/image.jpg", "https://example.com/thumb.jpg", null, TestUserId, null);

        await SeedLibraryEntryAsync(shareRequest.SourceGameId);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.MergeKnowledgeBase,
            TargetSharedGameId: existingGame.Id,
            AdminNotes: "Merging PDFs");

        _shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(TestPrivateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(existingGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        ShareRequest? capturedRequest = null;
        _shareRequestRepoMock
            .Setup(r => r.Update(It.IsAny<ShareRequest>()))
            .Callback<ShareRequest>(r => capturedRequest = r);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(existingGame.Id, response.TargetSharedGameId);

        // Verify NO new SharedGame was created
        _sharedGameRepoMock.Verify(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify ShareRequest was approved with existing game ID
        Assert.NotNull(capturedRequest);
        Assert.Equal(ShareRequestStatus.Approved, capturedRequest.Status);
        Assert.Equal(existingGame.Id, capturedRequest.TargetSharedGameId);
    }

    [Fact]
    public async Task Handle_WithMergeKnowledgeBase_NonExistentTargetGame_ThrowsNotFoundException()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview();
        var privateGame = CreatePrivateGame();

        await SeedLibraryEntryAsync(shareRequest.SourceGameId);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.MergeKnowledgeBase,
            TargetSharedGameId: Guid.NewGuid(), // Non-existent game
            AdminNotes: null);

        _shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(TestPrivateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithApproveAsVariant_CreatesGameWithVariantSuffix()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview();
        var privateGame = CreatePrivateGame();
        var baseGame = SharedGame.Create("Base Game", 2020, "A base game", 2, 4, 60, 10, 2.5m, null, "https://example.com/image.jpg", "https://example.com/thumb.jpg", null, TestUserId, null);

        await SeedLibraryEntryAsync(shareRequest.SourceGameId);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsVariant,
            TargetSharedGameId: baseGame.Id,
            AdminNotes: "Variant edition");

        _shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(TestPrivateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(baseGame.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseGame);

        SharedGame? capturedSharedGame = null;
        _sharedGameRepoMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((g, _) => capturedSharedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedSharedGame);
        Assert.EndsWith(" (Variant)", capturedSharedGame.Title);
        Assert.Equal($"{privateGame.Title} (Variant)", capturedSharedGame.Title);
        Assert.Null(capturedSharedGame.BggId); // Variants don't use BGG ID

        _sharedGameRepoMock.Verify(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithApproveAsVariant_NonExistentBaseGame_ThrowsNotFoundException()
    {
        // Arrange
        var shareRequest = CreateShareRequestInReview();
        var privateGame = CreatePrivateGame();

        await SeedLibraryEntryAsync(shareRequest.SourceGameId);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: TestAdminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsVariant,
            TargetSharedGameId: Guid.NewGuid(), // Non-existent base game
            AdminNotes: null);

        _shareRequestRepoMock
            .Setup(r => r.GetByIdForUpdateAsync(command.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(TestPrivateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateGame);

        _sharedGameRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    // Note: Document copying is tested in integration tests where we can properly seed the database

    private static ShareRequest CreateShareRequestInReview()
    {
        var shareRequest = ShareRequest.Create(
            TestUserId,
            TestLibraryEntryId, // SourceGameId references UserLibraryEntry.Id for NewGameProposal
            ContributionType.NewGameProposal,
            "Proposing new game");
        shareRequest.StartReview(TestAdminId);
        return shareRequest;
    }

    private static PrivateGame CreatePrivateGame()
    {
        return PrivateGame.CreateFromBgg(
            ownerId: TestUserId,
            bggId: 12345,
            title: "Test Private Game",
            description: "A test game",
            yearPublished: 2020,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m);
    }

    private async Task SeedLibraryEntryAsync(Guid libraryEntryId)
    {
        var libraryEntry = new UserLibraryEntryEntity
        {
            Id = libraryEntryId,
            UserId = TestUserId,
            PrivateGameId = TestPrivateGameId,
            AddedAt = DateTime.UtcNow,
                    };
        _dbContext.UserLibraryEntries.Add(libraryEntry);
        await _dbContext.SaveChangesAsync();
    }
}
