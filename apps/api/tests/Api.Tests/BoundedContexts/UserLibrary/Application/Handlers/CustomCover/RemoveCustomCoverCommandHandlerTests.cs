using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.CustomCover;

/// <summary>
/// Unit tests for RemoveCustomCoverCommandHandler.
/// Issue #1824 (L3 custom cover removal).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class RemoveCustomCoverCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo = new();
    private readonly Mock<IBlobStorageService> _mockBlobStorage = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<HybridCache> _mockCache = new();
    private readonly Mock<ILogger<RemoveCustomCoverCommandHandler>> _mockLogger = new();
    private readonly RemoveCustomCoverCommandHandler _handler;

    public RemoveCustomCoverCommandHandlerTests()
    {
        _handler = new RemoveCustomCoverCommandHandler(
            _mockLibraryRepo.Object,
            _mockBlobStorage.Object,
            _mockUnitOfWork.Object,
            _mockCache.Object,
            _mockLogger.Object);
    }

    private static UserLibraryEntry CreateEntry(Guid userId, Guid gameId)
        => new(Guid.NewGuid(), userId, gameId);

    private static RemoveCustomCoverCommand CreateCommand(Guid userId, Guid gameId)
        => new(userId, gameId);

    // -------------------------------------------------------
    // T3-1: game not in library → ForbiddenException
    // -------------------------------------------------------

    [Fact]
    public async Task Handle_GameNotInLibrary_ThrowsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        var command = CreateCommand(userId, gameId);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage($"*{gameId}*");
    }

    // -------------------------------------------------------
    // T3-2: no custom cover exists → NotFoundException
    // -------------------------------------------------------

    [Fact]
    public async Task Handle_NoCustomCoverExists_ThrowsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);

        // Ensure CustomCoverR2Key is null (default state)
        entry.SetCustomCoverR2Key(null);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var command = CreateCommand(userId, gameId);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*No custom cover exists*");
    }

    // -------------------------------------------------------
    // T3-3: happy path — deletes R2, nulls DB field
    // -------------------------------------------------------

    [Fact]
    public async Task Handle_HappyPath_DeletesR2_NullsDbField()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);
        var coverKey = $"user-covers/{userId}/{gameId}/cover";

        // Seed an existing custom cover
        entry.SetCustomCoverR2Key(coverKey);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockBlobStorage
            .Setup(b => b.DeleteRawKeyAsync($"{coverKey}.webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = CreateCommand(userId, gameId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — raw-key delete of the exact physical object (#3384)
        _mockBlobStorage.Verify(
            b => b.DeleteRawKeyAsync($"{coverKey}.webp", It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify SetCustomCoverR2Key was called with null
        entry.CustomCoverR2Key.Should().BeNull();

        _mockLibraryRepo.Verify(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------
    // T3-3b (#4499): the cached game detail is evicted, or it keeps offering «Rimuovi»
    // for up to 5 minutes and a second click answers 404.
    // -------------------------------------------------------

    [Fact]
    public async Task Handle_HappyPath_EvictsTheCachedGameDetail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);
        entry.SetCustomCoverR2Key($"user-covers/{userId}/{gameId}/cover");

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _mockBlobStorage
            .Setup(b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(CreateCommand(userId, gameId), TestContext.Current.CancellationToken);

        // Assert
        _mockCache.Verify(
            c => c.RemoveAsync(GetGameDetailQueryHandler.BuildCacheKey(userId, gameId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -------------------------------------------------------
    // T3-4: R2 delete throws → logs warning, still nulls DB
    // -------------------------------------------------------

    [Fact]
    public async Task Handle_R2DeleteThrows_LogsWarning_StillNullsDb()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);
        var coverKey = $"user-covers/{userId}/{gameId}/cover";

        // Seed an existing custom cover
        entry.SetCustomCoverR2Key(coverKey);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var deleteException = new InvalidOperationException("R2 service unavailable");
        _mockBlobStorage
            .Setup(b => b.DeleteRawKeyAsync($"{coverKey}.webp", It.IsAny<CancellationToken>()))
            .ThrowsAsync(deleteException);

        var command = CreateCommand(userId, gameId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert: handler should NOT propagate exception
        _mockBlobStorage.Verify(
            b => b.DeleteRawKeyAsync($"{coverKey}.webp", It.IsAny<CancellationToken>()),
            Times.Once);

        // DB should still be updated despite R2 failure
        entry.CustomCoverR2Key.Should().BeNull();

        _mockLibraryRepo.Verify(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
