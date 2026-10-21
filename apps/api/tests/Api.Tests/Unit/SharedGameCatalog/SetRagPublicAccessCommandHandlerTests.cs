using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Unit.SharedGameCatalog;

/// <summary>
/// Unit tests for SetRagPublicAccessCommandHandler.
/// Ownership/RAG access feature.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SetRagPublicAccessCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ILogger<SetRagPublicAccessCommandHandler>> _mockLogger;
    private readonly Mock<IHybridCacheService> _mockCache;
    private readonly Mock<ICacheInvalidationRetryPolicy> _mockRetryPolicy;
    private readonly Mock<HybridCache> _mockHybridCache = new();

    public SetRagPublicAccessCommandHandlerTests()
    {
        _mockRepo = new Mock<ISharedGameRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<SetRagPublicAccessCommandHandler>>();
        _mockCache = new Mock<IHybridCacheService>();
        _mockCache
            .Setup(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        // La retry policy ESEGUE il delegato (come in UpdateSharedGameCommandHandlerTests, #4100):
        // altrimenti il mock della cache non viene esercitato e un test sull'invalidazione passa
        // senza che nulla venga invalidato.
        _mockRetryPolicy = new Mock<ICacheInvalidationRetryPolicy>();
        _mockRetryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, ValueTask>, string, CancellationToken>((op, _, ct) => op(ct).AsTask());
        _mockHybridCache
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
    }

    private SetRagPublicAccessCommandHandler CreateHandler() =>
        new(
            _mockRepo.Object,
            _mockUow.Object,
            new SharedGameCacheInvalidator(_mockCache.Object, _mockRetryPolicy.Object, _mockHybridCache.Object),
            _mockLogger.Object);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ToggleOnOff_UpdatesAndSaves(bool isRagPublic)
    {
        // Arrange
        var game = CreateTestSharedGame();

        _mockRepo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = CreateHandler();
        var command = new SetRagPublicAccessCommand(game.Id, isRagPublic);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        game.IsRagPublic.Should().Be(isRagPublic);
        _mockRepo.Verify(r => r.Update(game), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var handler = CreateHandler();
        var command = new SetRagPublicAccessCommand(gameId, true);

        // Act & Assert
        await ((Func<Task>)(() => handler.Handle(command, CancellationToken.None))).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ToggleFromTrueToFalse_SetsCorrectly()
    {
        // Arrange — game starts with IsRagPublic = true
        var game = CreateTestSharedGame();
        game.SetRagPublicAccess(true);
        game.IsRagPublic.Should().BeTrue();

        _mockRepo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = CreateHandler();
        var command = new SetRagPublicAccessCommand(game.Id, false);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        game.IsRagPublic.Should().BeFalse();
    }

    /// <summary>
    /// #4585: la lista del catalogo mostra <c>isRagPublic</c> e vive in HybridCache sotto il tag
    /// <c>search-games</c>. Senza l'evizione dopo il salvataggio serviva il valore precedente per la
    /// durata della cache. La prova end-to-end è
    /// <c>SetRagPublicAccess_ThenTheCatalogList_ShowsTheNewValue</c>, su Postgres.
    /// </summary>
    [Fact]
    public async Task Handle_AfterSave_EvictsListAndDetailReadModelCaches()
    {
        var game = CreateTestSharedGame();
        _mockRepo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var saved = false;
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => saved = true)
            .ReturnsAsync(1);
        var evictedBeforeSave = false;
        _mockCache
            .Setup(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => evictedBeforeSave |= !saved)
            .ReturnsAsync(0);
        _mockHybridCache
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => evictedBeforeSave |= !saved)
            .Returns(ValueTask.CompletedTask);

        await CreateHandler().Handle(new SetRagPublicAccessCommand(game.Id, true), CancellationToken.None);

        _mockCache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync("search-games", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync($"shared-game:{game.Id}", It.IsAny<CancellationToken>()), Times.Once);
        // La lista è scritta con HybridCache grezzo, che il wrapper non raggiunge (#4561): serve il tag nativo.
        _mockHybridCache.Verify(
            c => c.RemoveByTagAsync("search-games", It.IsAny<CancellationToken>()), Times.Once);
        // Il dettaglio non riporta `isRagPublic`, ma questo handler lo evince da sempre insieme alla lista
        // (via #4100): resta, e ora ha anche il passo nativo.
        _mockHybridCache.Verify(
            c => c.RemoveByTagAsync($"shared-game:{game.Id}", It.IsAny<CancellationToken>()), Times.Once);
        evictedBeforeSave.Should().BeFalse("evict prima del salvataggio lascerebbe che una lettura ripopoli la cache col valore vecchio");
    }

    [Fact]
    public async Task Handle_GameNotFound_EvictsNothing()
    {
        var gameId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>())).ReturnsAsync((SharedGame?)null);

        await ((Func<Task>)(() => CreateHandler().Handle(new SetRagPublicAccessCommand(gameId, true), CancellationToken.None)))
            .Should().ThrowAsync<NotFoundException>();

        _mockCache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockHybridCache.Verify(
            c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #region Helpers

    private static SharedGame CreateTestSharedGame()
    {
        return SharedGame.Create(
            title: "Test Game",
            yearPublished: 2024,
            description: "A test game for unit tests",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 12,
            complexityRating: 2.5m,
            averageRating: 7.0m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid());
    }

    #endregion
}
