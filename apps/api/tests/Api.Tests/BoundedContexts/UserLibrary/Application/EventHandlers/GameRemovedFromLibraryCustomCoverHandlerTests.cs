using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.EventHandlers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GameRemovedFromLibraryCustomCoverHandlerTests
{
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<ILogger<GameRemovedFromLibraryCustomCoverHandler>> _logger = new();

    private GameRemovedFromLibraryCustomCoverHandler Handler() =>
        new(_blob.Object, _logger.Object);

    private static GameRemovedFromLibraryEvent MakeEvent(string? coverKey = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), coverKey);

    // ── T4-1 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_CoverExists_DeletesR2WithCorrectArguments()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var resourceKey = $"user-covers/{userId}/{gameId}/cover";
        var expectedObjectKey = $"{resourceKey}.webp";

        var evt = new GameRemovedFromLibraryEvent(Guid.NewGuid(), userId, gameId, resourceKey);
        _blob.Setup(b => b.DeleteAsync(expectedObjectKey, BlobCategory.GameImage, resourceKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        // Act
        await Handler().Handle(evt, default);

        // Assert — DeleteAsync called exactly once with ToObjectKey(resourceKey), GameImage, resourceKey
        _blob.Verify(b => b.DeleteAsync(
            expectedObjectKey,
            BlobCategory.GameImage,
            resourceKey,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── T4-2 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_NoCover_NoOpDoesNotCallDeleteAsync()
    {
        // Arrange — null CustomCoverR2Key
        var evt = MakeEvent(coverKey: null);

        // Act
        await Handler().Handle(evt, default);

        // Assert — DeleteAsync must never be called
        _blob.Verify(
            b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhitespaceCover_NoOpDoesNotCallDeleteAsync()
    {
        // Arrange — whitespace CustomCoverR2Key is treated as absent
        var evt = MakeEvent(coverKey: "   ");

        // Act
        await Handler().Handle(evt, default);

        // Assert
        _blob.Verify(
            b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── T4-3 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_R2Throws_LogsWarningAndDoesNotPropagate()
    {
        // Arrange
        var resourceKey = $"user-covers/{Guid.NewGuid()}/{Guid.NewGuid()}/cover";
        var evt = MakeEvent(resourceKey);
        _blob.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("S3 endpoint unreachable"));

        // Act
        var act = async () => await Handler().Handle(evt, default);

        // Assert — no exception propagated
        await act.Should().NotThrowAsync();

        // Warning should be logged once
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(resourceKey)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ── T4-4 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_Cancellation_PropagatesOperationCanceledException()
    {
        // Arrange
        var resourceKey = $"user-covers/{Guid.NewGuid()}/{Guid.NewGuid()}/cover";
        var evt = MakeEvent(resourceKey);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _blob.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), cts.Token))
             .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var act = async () => await Handler().Handle(evt, cts.Token);

        // Assert — OperationCanceledException must propagate
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
