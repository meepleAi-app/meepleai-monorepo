using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class GetSharedGameByIdQueryHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly HybridCache _cache;
    private readonly Mock<ILogger<GetSharedGameByIdQueryHandler>> _loggerMock;
    private readonly GetSharedGameByIdQueryHandler _handler;

    public GetSharedGameByIdQueryHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _loggerMock = new Mock<ILogger<GetSharedGameByIdQueryHandler>>();

        // Use real HybridCache for tests (with in-memory backend)
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddHybridCache();
        var serviceProvider = services.BuildServiceProvider();
        _cache = serviceProvider.GetRequiredService<HybridCache>();

        _handler = new GetSharedGameByIdQueryHandler(
            _repositoryMock.Object,
            _cache,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingGame_ReturnsDetailDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var game = SharedGame.Create(
            "Catan",
            1995,
            "Description",
            3,
            4,
            90,
            10,
            2.5m,
            7.8m,
            "https://example.com/catan.jpg",
            "https://example.com/thumb.jpg",
            GameRules.Create("Rules content", "en"),
            userId,
            13);

        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(game.Id);
        result.Title.Should().Be("Catan");
        result.YearPublished.Should().Be(1995);
        result.MinPlayers.Should().Be(3);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTimeMinutes.Should().Be(90);
        result.MinAge.Should().Be(10);
        result.ComplexityRating.Should().Be(2.5m);
        result.AverageRating.Should().Be(7.8m);
        result.BggId.Should().Be(13);
        result.Rules.Should().NotBeNull();
        result.Rules.Content.Should().Be("Rules content");
        result.Rules.Language.Should().Be("en");
        result.CreatedBy.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithGameWithoutRules_ReturnsNullRules()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = SharedGame.Create(
            "Simple Game",
            2020,
            "Description",
            2,
            4,
            60,
            8,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Rules.Should().BeNull();
    }
}
