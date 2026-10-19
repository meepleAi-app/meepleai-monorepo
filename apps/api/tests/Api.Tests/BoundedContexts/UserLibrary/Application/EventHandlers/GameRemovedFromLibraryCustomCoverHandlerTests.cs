using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
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
    private readonly Mock<IUserLibraryRepository> _libraryRepo = new();
    private readonly Mock<ILogger<GameRemovedFromLibraryCustomCoverHandler>> _logger = new();

    private GameRemovedFromLibraryCustomCoverHandler Handler() =>
        new(_blob.Object, _libraryRepo.Object, _logger.Object);

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
        _blob.Setup(b => b.DeleteRawKeyAsync(expectedObjectKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        // Act
        await Handler().Handle(evt, default);

        // Assert — raw-key delete of the exact physical object (#3384)
        _blob.Verify(b => b.DeleteRawKeyAsync(
            expectedObjectKey,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── T4-1b (#4499) ────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_KeyStillUsedByAnEntry_DoesNotDelete()
    {
        // Arrange — the game was added again and got a new cover before the outbox delivered
        // the removal: the deterministic key is the same, and the object is the new cover.
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var resourceKey = $"user-covers/{userId}/{gameId}/cover";
        _libraryRepo
            .Setup(r => r.IsCustomCoverKeyInUseAsync(resourceKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await Handler().Handle(new GameRemovedFromLibraryEvent(Guid.NewGuid(), userId, gameId, resourceKey), default);

        // Assert
        _blob.Verify(
            b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
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
            b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── T4-3 ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_R2Throws_LogsWarningAndDoesNotPropagate()
    {
        // Arrange
        var resourceKey = $"user-covers/{Guid.NewGuid()}/{Guid.NewGuid()}/cover";
        var evt = MakeEvent(resourceKey);
        _blob.Setup(b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

        _blob.Setup(b => b.DeleteRawKeyAsync(It.IsAny<string>(), cts.Token))
             .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var act = async () => await Handler().Handle(evt, cts.Token);

        // Assert — OperationCanceledException must propagate
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
