using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Unit.UserLibrary;

/// <summary>
/// Unit tests for DeclareOwnershipCommandHandler.
/// Ownership/RAG access feature.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class DeclareOwnershipCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();

    private readonly Mock<IUserLibraryRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IRagAccessService> _mockRagService;
    private readonly Mock<ILogger<DeclareOwnershipCommandHandler>> _mockLogger;

    public DeclareOwnershipCommandHandlerTests()
    {
        _mockRepo = new Mock<IUserLibraryRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockRagService = new Mock<IRagAccessService>();
        _mockLogger = new Mock<ILogger<DeclareOwnershipCommandHandler>>();
    }

    [Fact]
    public async Task Handle_NuovoState_TransitionsToOwnedAndReturnsResult()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), UserId, GameId);
        // Entry starts in Nuovo state by default

        var db = CreateDbWithSharedGame(isRagPublic: false);

        _mockRepo.Setup(r => r.GetByUserAndGameAsync(UserId, GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _mockRepo.Setup(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockRagService.Setup(r => r.CanAccessRagAsync(UserId, GameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockRagService.Setup(r => r.GetAccessibleKbCardsAsync(UserId, GameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });

        var handler = new DeclareOwnershipCommandHandler(_mockRepo.Object, _mockUow.Object, _mockRagService.Object, db, _mockLogger.Object);
        var command = new DeclareOwnershipCommand(UserId, GameId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Owned", result.GameState);
        Assert.NotNull(result.OwnershipDeclaredAt);
        Assert.True(result.HasRagAccess);
        Assert.Equal(2, result.KbCardCount);
        _mockRepo.Verify(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GameNotInLibrary_ThrowsNotFoundException()
    {
        // Arrange
        var db = CreateDbWithSharedGame(isRagPublic: false);

        _mockRepo.Setup(r => r.GetByUserAndGameAsync(UserId, GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        var handler = new DeclareOwnershipCommandHandler(_mockRepo.Object, _mockUow.Object, _mockRagService.Object, db, _mockLogger.Object);
        var command = new DeclareOwnershipCommand(UserId, GameId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WishlistState_ThrowsDomainException()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), UserId, GameId);
        // Transition to Wishlist
        entry.AddToWishlist();

        var db = CreateDbWithSharedGame(isRagPublic: false);

        _mockRepo.Setup(r => r.GetByUserAndGameAsync(UserId, GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var handler = new DeclareOwnershipCommandHandler(_mockRepo.Object, _mockUow.Object, _mockRagService.Object, db, _mockLogger.Object);
        var command = new DeclareOwnershipCommand(UserId, GameId);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyDeclared_ReturnsCurrentStateIdempotently()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), UserId, GameId);
        // Declare ownership first time
        entry.DeclareOwnership();
        var originalDeclaredAt = entry.OwnershipDeclaredAt;

        var db = CreateDbWithSharedGame(isRagPublic: true);

        _mockRepo.Setup(r => r.GetByUserAndGameAsync(UserId, GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _mockRepo.Setup(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockRagService.Setup(r => r.CanAccessRagAsync(UserId, GameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockRagService.Setup(r => r.GetAccessibleKbCardsAsync(UserId, GameId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var handler = new DeclareOwnershipCommandHandler(_mockRepo.Object, _mockUow.Object, _mockRagService.Object, db, _mockLogger.Object);
        var command = new DeclareOwnershipCommand(UserId, GameId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — should succeed without error and return the same declared-at timestamp
        Assert.Equal("Owned", result.GameState);
        Assert.Equal(originalDeclaredAt, result.OwnershipDeclaredAt);
        Assert.True(result.IsRagPublic);
    }

    #region Helpers

    private static MeepleAiDbContext CreateDbWithSharedGame(bool isRagPublic)
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = GameId,
            Title = "Test Game",
            IsRagPublic = isRagPublic,
            IsDeleted = false,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        return db;
    }

    #endregion
}
