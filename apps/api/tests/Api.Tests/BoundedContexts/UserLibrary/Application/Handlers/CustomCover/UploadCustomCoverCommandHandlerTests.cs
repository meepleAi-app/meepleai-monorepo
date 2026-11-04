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
/// Unit tests for UploadCustomCoverCommandHandler.
/// Issue #1824 (L3 custom cover upload).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class UploadCustomCoverCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo = new();
    private readonly Mock<IBlobStorageService> _mockBlobStorage = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<HybridCache> _mockCache = new();
    private readonly Mock<ILogger<UploadCustomCoverCommandHandler>> _mockLogger = new();
    private readonly UploadCustomCoverCommandHandler _handler;

    public UploadCustomCoverCommandHandlerTests()
    {
        _handler = new UploadCustomCoverCommandHandler(
            _mockLibraryRepo.Object,
            _mockBlobStorage.Object,
            _mockUnitOfWork.Object,
            _mockCache.Object,
            _mockLogger.Object);
    }

    private static UserLibraryEntry CreateEntry(Guid userId, Guid gameId)
        => new(Guid.NewGuid(), userId, gameId);

    private static UploadCustomCoverCommand CreateCommand(Guid userId, Guid gameId)
        => new(userId, gameId, Stream.Null, FileSizeBytes: 1024, MimeType: "image/webp");

    // -------------------------------------------------------
    // T2-1: game not in library → ForbiddenException
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
    // T2-2: happy path — writes to R2 via the RAW-KEY deterministic path (#3384),
    // saves DB, returns presigned URL for the exact physical key.
    // -------------------------------------------------------

    [Fact]
    public async Task Handle_HappyPath_UploadsToR2_UpdatesDb_ReturnsPresignedUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);
        var expectedUrl = "https://r2.example.com/presigned-url";
        var expectedKey = $"user-covers/{userId}/{gameId}/cover";
        var physicalKey = $"{expectedKey}.webp";

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockBlobStorage
            .Setup(b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockBlobStorage
            .Setup(b => b.GetPresignedUrlForRawKeyAsync(physicalKey, 3600))
            .ReturnsAsync(expectedUrl);

        var command = CreateCommand(userId, gameId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.CoverR2Key.Should().Be(expectedKey);
        result.PresignedUrl.Should().Be(expectedUrl);

        // The deterministic physical key is written verbatim — NOT the categorized
        // StoreAsync (which would reject the '/' and mint a random-id key the resolver
        // cannot reconstruct).
        _mockBlobStorage.Verify(
            b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockBlobStorage.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mockLibraryRepo.Verify(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------
    // T2-3 (#4499): replace flow. The key is deterministic, so the old cover and the new one
    // are the SAME object: the write overwrites it. Deleting it first lost the old cover
    // whenever the write then failed, while the DB still pointed at it.
    // -------------------------------------------------------

    [Fact]
    public async Task Handle_ReplaceExistingCover_SameKey_OverwritesWithoutDeleting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);
        var key = $"user-covers/{userId}/{gameId}/cover";
        var physicalKey = $"{key}.webp";

        // Seed an existing custom cover on the entry
        entry.SetCustomCoverR2Key(key);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockBlobStorage
            .Setup(b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockBlobStorage
            .Setup(b => b.GetPresignedUrlForRawKeyAsync(physicalKey, 3600))
            .ReturnsAsync("https://r2.example.com/new-url");

        var command = CreateCommand(userId, gameId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert: one write over the same object, no delete
        _mockBlobStorage.Verify(
            b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockBlobStorage.Verify(
            b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ReplaceExistingCover_SameKey_WriteFails_KeepsTheOldCover()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);
        var key = $"user-covers/{userId}/{gameId}/cover";
        entry.SetCustomCoverR2Key(key);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockBlobStorage
            .Setup(b => b.StoreRawKeyAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var act = () => _handler.Handle(CreateCommand(userId, gameId), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ExternalServiceException>();
        _mockBlobStorage.Verify(
            b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the old cover is the object the DB still points at: deleting it before a failed write loses it");
        entry.CustomCoverR2Key.Should().Be(key);
    }

    [Fact]
    public async Task Handle_ReplaceCoverStoredUnderAnotherKey_DeletesTheOldOneOnlyAfterTheWrite()
    {
        // Arrange: a key in a format other than the current CoverKeyBuilder.ForUser one.
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);
        const string legacyKey = "user-covers/legacy-layout/cover";
        entry.SetCustomCoverR2Key(legacyKey);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var calls = new List<string>();
        _mockBlobStorage
            .Setup(b => b.StoreRawKeyAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string k, Stream _, string _, CancellationToken _) => calls.Add($"store {k}"))
            .ReturnsAsync(true);
        _mockBlobStorage
            .Setup(b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string k, CancellationToken _) => calls.Add($"delete {k}"))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(CreateCommand(userId, gameId), TestContext.Current.CancellationToken);

        // Assert
        calls.Should().Equal(
            $"store user-covers/{userId}/{gameId}/cover.webp",
            $"delete {legacyKey}.webp");
        entry.CustomCoverR2Key.Should().Be($"user-covers/{userId}/{gameId}/cover");
    }

    [Fact]
    public async Task Handle_HappyPath_EvictsTheCachedGameDetail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _mockBlobStorage
            .Setup(b => b.StoreRawKeyAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(CreateCommand(userId, gameId), TestContext.Current.CancellationToken);

        // Assert: the detail (GET /library/games/{gameId}) is cached for 5 minutes; without the
        // eviction it keeps answering with the key it had before the upload.
        _mockCache.Verify(
            c => c.RemoveAsync(GetGameDetailQueryHandler.BuildCacheKey(userId, gameId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -------------------------------------------------------
    // T2-4 (#3384): a failed raw-key write (false — S3 error or local-storage mode)
    // must NOT persist an unresolvable key. Throw ExternalServiceException and leave
    // the DB untouched.
    // -------------------------------------------------------

    [Fact]
    public async Task Handle_RawKeyWriteFails_ThrowsAndDoesNotPersist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);
        var physicalKey = $"user-covers/{userId}/{gameId}/cover.webp";

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockBlobStorage
            .Setup(b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = CreateCommand(userId, gameId);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ExternalServiceException>();

        entry.CustomCoverR2Key.Should().BeNull("no key is persisted when the object write failed");
        _mockLibraryRepo.Verify(r => r.UpdateAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
