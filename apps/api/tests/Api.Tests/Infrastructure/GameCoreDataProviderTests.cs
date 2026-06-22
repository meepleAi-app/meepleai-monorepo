using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="GameCoreDataProvider"/>.
/// Uses Moq to isolate the provider from the actual repositories.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GameCoreDataProviderTests
{
    private readonly Mock<ISharedGameRepository> _sharedRepoMock;
    private readonly Mock<IPrivateGameRepository> _privateRepoMock;
    private readonly GameCoreDataProvider _provider;

    // Reusable test data
    private static readonly Guid _ownerId = Guid.NewGuid();
    private static readonly Guid _sharedGameId = Guid.NewGuid();
    private static readonly Guid _privateGameId = Guid.NewGuid();

    public GameCoreDataProviderTests()
    {
        _sharedRepoMock = new Mock<ISharedGameRepository>();
        _privateRepoMock = new Mock<IPrivateGameRepository>();
        _provider = new GameCoreDataProvider(_sharedRepoMock.Object, _privateRepoMock.Object);
    }

    // ── SharedGame resolution ──────────────────────────────────────────────

    [Fact]
    public async Task GetCoreDataAsync_SharedKind_ReturnsSharedGameData()
    {
        // Arrange
        var sg = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "A classic settler game",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.8m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan-thumb.jpg",
            rules: GameRules.Create("Rule text", "en"),
            createdBy: _ownerId,
            bggId: 13);

        _sharedRepoMock
            .Setup(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sg);

        var gameRef = GameRef.Shared(_sharedGameId);

        // Act
        var result = await _provider.GetCoreDataAsync(gameRef);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Catan");
        result.YearPublished.Should().Be(1995);
        result.MinPlayers.Should().Be(3);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTimeMinutes.Should().Be(90);
        result.MinAge.Should().Be(10);
        result.Description.Should().Be("A classic settler game");
        result.BggId.Should().Be(13);
        result.ComplexityRating.Should().Be(2.5m);

        _sharedRepoMock.Verify(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>()), Times.Once);
        _privateRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCoreDataAsync_SharedKind_UnknownId_ReturnsNull()
    {
        // Arrange
        _sharedRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var gameRef = GameRef.Shared(Guid.NewGuid());

        // Act
        var result = await _provider.GetCoreDataAsync(gameRef);

        // Assert
        result.Should().BeNull();
    }

    // ── PrivateGame resolution ─────────────────────────────────────────────

    [Fact]
    public async Task GetCoreDataAsync_PrivateKind_ReturnsPrivateGameData()
    {
        // Arrange
        var pg = PrivateGame.CreateFromBgg(
            ownerId: _ownerId,
            bggId: 42,
            title: "Homebrew",
            yearPublished: 2020,
            description: "A custom game",
            minPlayers: 2,
            maxPlayers: 5,
            playingTimeMinutes: 60,
            minAge: 12,
            complexityRating: 3.0m,
            imageUrl: "https://example.com/homebrew.jpg",
            thumbnailUrl: "https://example.com/homebrew-thumb.jpg");

        _privateRepoMock
            .Setup(r => r.GetByIdAsync(_privateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pg);

        var gameRef = GameRef.Private(_privateGameId);

        // Act
        var result = await _provider.GetCoreDataAsync(gameRef);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Homebrew");
        result.YearPublished.Should().Be(2020);
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(5);
        result.PlayingTimeMinutes.Should().Be(60);
        result.MinAge.Should().Be(12);
        result.BggId.Should().Be(42);
        result.ComplexityRating.Should().Be(3.0m);

        _privateRepoMock.Verify(r => r.GetByIdAsync(_privateGameId, It.IsAny<CancellationToken>()), Times.Once);
        _sharedRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCoreDataAsync_PrivateKind_NullableFieldsDefault_MappedCorrectly()
    {
        // Arrange: PrivateGame.CreateManual omits nullable fields → check defaults map to 0
        var pg = PrivateGame.CreateManual(
            ownerId: _ownerId,
            title: "Minimal Game",
            minPlayers: 1,
            maxPlayers: 2);

        _privateRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pg);

        var gameRef = GameRef.Private(Guid.NewGuid());

        // Act
        var result = await _provider.GetCoreDataAsync(gameRef);

        // Assert: nullable int? fields default to 0 when null
        result.Should().NotBeNull();
        result!.Title.Should().Be("Minimal Game");
        result.YearPublished.Should().Be(0);
        result.PlayingTimeMinutes.Should().Be(0);
        result.MinAge.Should().Be(0);
        result.Description.Should().BeNull();
        result.ComplexityRating.Should().BeNull();
    }

    [Fact]
    public async Task GetCoreDataAsync_PrivateKind_UnknownId_ReturnsNull()
    {
        // Arrange
        _privateRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        var gameRef = GameRef.Private(Guid.NewGuid());

        // Act
        var result = await _provider.GetCoreDataAsync(gameRef);

        // Assert
        result.Should().BeNull();
    }

    // ── Batch resolution ───────────────────────────────────────────────────

    [Fact]
    public async Task GetCoreDataBatchAsync_ReturnsOnlyFoundEntries()
    {
        // Arrange: one shared game found, one private game found, one shared game NOT found
        var sg = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Description",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/img.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: _ownerId);

        var pg = PrivateGame.CreateManual(
            ownerId: _ownerId,
            title: "My Game",
            minPlayers: 2,
            maxPlayers: 4);

        var sharedRef = GameRef.Shared(_sharedGameId);
        var privateRef = GameRef.Private(_privateGameId);
        var missingRef = GameRef.Shared(Guid.NewGuid());

        _sharedRepoMock
            .Setup(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sg);
        _sharedRepoMock
            .Setup(r => r.GetByIdAsync(It.Is<Guid>(id => id != _sharedGameId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);
        _privateRepoMock
            .Setup(r => r.GetByIdAsync(_privateGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pg);

        // Act
        var result = await _provider.GetCoreDataBatchAsync([sharedRef, privateRef, missingRef]);

        // Assert: 2 found, 1 missing (missingRef is absent)
        result.Should().HaveCount(2);
        result.Should().ContainKey(sharedRef);
        result.Should().ContainKey(privateRef);
        result.Should().NotContainKey(missingRef);
        result[sharedRef].Title.Should().Be("Catan");
        result[privateRef].Title.Should().Be("My Game");
    }

    [Fact]
    public async Task GetCoreDataBatchAsync_DeduplicatesRefs()
    {
        // Arrange: pass the same ref twice — only one repo call expected
        var sg = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Description",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/img.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: _ownerId);

        _sharedRepoMock
            .Setup(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sg);

        var sharedRef = GameRef.Shared(_sharedGameId);

        // Act: duplicate ref
        var result = await _provider.GetCoreDataBatchAsync([sharedRef, sharedRef]);

        // Assert: 1 result, repo called once (deduplicated by Distinct)
        result.Should().HaveCount(1);
        _sharedRepoMock.Verify(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Null-guard ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCoreDataAsync_NullGameRef_Throws()
    {
        var act = () => _provider.GetCoreDataAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetCoreDataBatchAsync_NullCollection_Throws()
    {
        var act = () => _provider.GetCoreDataBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
