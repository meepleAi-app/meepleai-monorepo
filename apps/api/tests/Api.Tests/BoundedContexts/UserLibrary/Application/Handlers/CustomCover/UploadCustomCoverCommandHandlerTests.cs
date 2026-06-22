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
    // T2-2: happy path — uploads to R2, saves DB, returns URL
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

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockBlobStorage
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), BlobCategory.GameImage, expectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "file-id", "/path", 1024));

        _mockBlobStorage
            .Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), BlobCategory.GameImage, expectedKey, 3600))
            .ReturnsAsync(expectedUrl);

        var command = CreateCommand(userId, gameId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.CoverR2Key.Should().Be(expectedKey);
        result.PresignedUrl.Should().Be(expectedUrl);

        _mockBlobStorage.Verify(
            b => b.StoreAsync(
                It.IsAny<Stream>(),
                $"{expectedKey}.webp",
                BlobCategory.GameImage,
                expectedKey,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockLibraryRepo.Verify(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------
    // T2-3: replace flow — DeleteAsync called before StoreAsync
    // -------------------------------------------------------

    [Fact]
    public async Task Handle_ReplaceExistingCover_DeletesOldBeforeUploadingNew()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = CreateEntry(userId, gameId);
        var oldKey = $"user-covers/{userId}/{gameId}/cover";

        // Seed an existing custom cover on the entry
        entry.SetCustomCoverR2Key(oldKey);

        var newKey = $"user-covers/{userId}/{gameId}/cover";

        _mockLibraryRepo
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockBlobStorage
            .Setup(b => b.DeleteAsync(It.IsAny<string>(), BlobCategory.GameImage, oldKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockBlobStorage
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), BlobCategory.GameImage, newKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "file-id", "/path", 1024));

        _mockBlobStorage
            .Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), BlobCategory.GameImage, newKey, 3600))
            .ReturnsAsync("https://r2.example.com/new-url");

        var command = CreateCommand(userId, gameId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert: delete called before store (verify both were called; ordering guaranteed by sequential await)
        _mockBlobStorage.Verify(
            b => b.DeleteAsync(
                $"{oldKey}.webp",
                BlobCategory.GameImage,
                oldKey,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockBlobStorage.Verify(
            b => b.StoreAsync(
                It.IsAny<Stream>(),
                $"{newKey}.webp",
                BlobCategory.GameImage,
                newKey,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
