using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
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
    private readonly Mock<ILogger<UploadCustomCoverCommandHandler>> _mockLogger = new();
    private readonly UploadCustomCoverCommandHandler _handler;

    public UploadCustomCoverCommandHandlerTests()
    {
        _handler = new UploadCustomCoverCommandHandler(
            _mockLibraryRepo.Object,
            _mockBlobStorage.Object,
            _mockUnitOfWork.Object,
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
    // T2-3: replace flow — raw-key delete of the old cover before the raw-key write.
    // -------------------------------------------------------

    [Fact]
    public async Task Handle_ReplaceExistingCover_DeletesOldBeforeUploadingNew()
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
            .Setup(b => b.DeleteRawKeyAsync(physicalKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockBlobStorage
            .Setup(b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockBlobStorage
            .Setup(b => b.GetPresignedUrlForRawKeyAsync(physicalKey, 3600))
            .ReturnsAsync("https://r2.example.com/new-url");

        var command = CreateCommand(userId, gameId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert: delete before store (both via the raw-key path; ordering guaranteed by sequential await)
        _mockBlobStorage.Verify(
            b => b.DeleteRawKeyAsync(physicalKey, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockBlobStorage.Verify(
            b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()),
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
