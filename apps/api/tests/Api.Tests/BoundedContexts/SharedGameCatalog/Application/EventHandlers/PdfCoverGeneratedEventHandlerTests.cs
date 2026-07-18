using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class PdfCoverGeneratedEventHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IHybridCacheService> _cache = new();
    private readonly Mock<ILogger<PdfCoverGeneratedEventHandler>> _logger = new();

    public PdfCoverGeneratedEventHandlerTests()
    {
        _cache.Setup(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(0);
    }

    private PdfCoverGeneratedEventHandler Handler() =>
        new(_repo.Object, _uow.Object, _cache.Object, new PassthroughRetryPolicy(), _logger.Object);

    [Fact]
    public async Task Handle_NullSharedGameId_SkipsAndDoesNotCallRepo()
    {
        // Arrange
        var evt = new PdfCoverGeneratedEvent(Guid.NewGuid(), sharedGameId: null, "key", 0);

        // Act
        await Handler().Handle(evt, default);

        // Assert
        _repo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SharedGameNotFound_LogsAndReturns()
    {
        // Arrange
        var sgId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(sgId, It.IsAny<CancellationToken>()))
             .ReturnsAsync((SharedGame?)null);
        var evt = new PdfCoverGeneratedEvent(Guid.NewGuid(), sgId, "key", 0);

        // Act
        await Handler().Handle(evt, default);

        // Assert
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HappyPath_SetsKeyAndSaves()
    {
        // Arrange
        var sgId = Guid.NewGuid();
        var game = CreateValidGame();
        _repo.Setup(r => r.GetByIdAsync(sgId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(game);
        var evt = new PdfCoverGeneratedEvent(Guid.NewGuid(), sgId, "new-key", 0);

        // Act
        await Handler().Handle(evt, default);

        // Assert
        game.PdfCoverR2Key.Should().Be("new-key");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadySameKey_SkipsSave()
    {
        // Arrange
        var sgId = Guid.NewGuid();
        var game = CreateValidGame();
        game.SetPdfCoverR2Key("existing-key");
        _repo.Setup(r => r.GetByIdAsync(sgId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(game);
        var evt = new PdfCoverGeneratedEvent(Guid.NewGuid(), sgId, "existing-key", 0);

        // Act
        await Handler().Handle(evt, default);

        // Assert
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DifferentKey_Overwrites()
    {
        // Arrange
        var sgId = Guid.NewGuid();
        var game = CreateValidGame();
        game.SetPdfCoverR2Key("old-key");
        _repo.Setup(r => r.GetByIdAsync(sgId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(game);
        var evt = new PdfCoverGeneratedEvent(Guid.NewGuid(), sgId, "new-key", 0);

        // Act
        await Handler().Handle(evt, default);

        // Assert
        game.PdfCoverR2Key.Should().Be("new-key");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DifferentKey_InvalidatesSearchAndDetailCache()
    {
        // Issue #3143: propagating a new PDF cover onto an existing game must evict the
        // catalog read-model caches so the new coverUrl is served immediately.
        var sgId = Guid.NewGuid();
        var game = CreateValidGame();
        game.SetPdfCoverR2Key("old-key");
        _repo.Setup(r => r.GetByIdAsync(sgId, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var evt = new PdfCoverGeneratedEvent(Guid.NewGuid(), sgId, "new-key", 0);

        await Handler().Handle(evt, default);

        _cache.Verify(c => c.RemoveByTagAcrossReplicasAsync("search-games", It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveByTagAcrossReplicasAsync($"shared-game:{sgId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadySameKey_DoesNotInvalidateCache()
    {
        // No-op path (key unchanged) must NOT evict.
        var sgId = Guid.NewGuid();
        var game = CreateValidGame();
        game.SetPdfCoverR2Key("existing-key");
        _repo.Setup(r => r.GetByIdAsync(sgId, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var evt = new PdfCoverGeneratedEvent(Guid.NewGuid(), sgId, "existing-key", 0);

        await Handler().Handle(evt, default);

        _cache.Verify(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Inlined from SharedGameTests.CreateValidGame (same assembly, Task 5 pattern).
    private static SharedGame CreateValidGame() =>
        SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test description",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid());
}
