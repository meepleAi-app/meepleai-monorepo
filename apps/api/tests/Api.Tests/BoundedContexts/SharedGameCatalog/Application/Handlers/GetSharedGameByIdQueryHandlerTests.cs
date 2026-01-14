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

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class GetSharedGameByIdQueryHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly HybridCache _cache;
    private readonly Mock<ICacheMetricsRecorder> _cacheMetricsMock;
    private readonly Mock<ILogger<GetSharedGameByIdQueryHandler>> _loggerMock;
    private readonly GetSharedGameByIdQueryHandler _handler;

    public GetSharedGameByIdQueryHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _cacheMetricsMock = new Mock<ICacheMetricsRecorder>();
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
            _cacheMetricsMock.Object,
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
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.Id);
        Assert.Equal("Catan", result.Title);
        Assert.Equal(1995, result.YearPublished);
        Assert.Equal(3, result.MinPlayers);
        Assert.Equal(4, result.MaxPlayers);
        Assert.Equal(90, result.PlayingTimeMinutes);
        Assert.Equal(10, result.MinAge);
        Assert.Equal(2.5m, result.ComplexityRating);
        Assert.Equal(7.8m, result.AverageRating);
        Assert.Equal(13, result.BggId);
        Assert.NotNull(result.Rules);
        Assert.Equal("Rules content", result.Rules.Content);
        Assert.Equal("en", result.Rules.Language);
        Assert.Equal(userId, result.CreatedBy);
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
        Assert.Null(result);
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
        Assert.NotNull(result);
        Assert.Null(result.Rules);
    }
}
