using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;

[Trait("Category", TestCategories.Unit)]
public class GetCatalogTrendingQueryHandlerTests
{
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<ILogger<GetCatalogTrendingQueryHandler>> _loggerMock;

    public GetCatalogTrendingQueryHandlerTests()
    {
        _cacheMock = new Mock<IHybridCacheService>();
        _loggerMock = new Mock<ILogger<GetCatalogTrendingQueryHandler>>();
    }

    private MeepleAiDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    [Fact]
    public async Task Handle_WithCachedData_ReturnsCachedResult()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var cachedTrending = new List<TrendingGameDto>
        {
            new() { Rank = 1, GameId = Guid.NewGuid(), Title = "Cached Game", Score = 50.0 }
        };

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                "catalog:trending",
                It.IsAny<Func<CancellationToken, Task<List<TrendingGameDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedTrending);

        var handler = new GetCatalogTrendingQueryHandler(context, _cacheMock.Object, _loggerMock.Object);

        // Act
        var result = await handler.Handle(new GetCatalogTrendingQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Cached Game");
    }

    [Fact]
    public async Task Handle_WithNoEvents_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Setup cache to execute factory
        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TrendingGameDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<List<TrendingGameDto>>>, string[], TimeSpan?, CancellationToken>(
                (_, factory, _, _, ct) => factory(ct));

        var handler = new GetCatalogTrendingQueryHandler(context, _cacheMock.Object, _loggerMock.Object);

        // Act
        var result = await handler.Handle(new GetCatalogTrendingQuery(10), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithEvents_ReturnsRankedTrending()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var game1Id = Guid.NewGuid();
        var game2Id = Guid.NewGuid();

        // Game 1: 3 plays (10 weight each)
        context.Set<GameAnalyticsEventEntity>().AddRange(
            CreateEvent(game1Id, GameEventType.Play, 0),
            CreateEvent(game1Id, GameEventType.Play, 1),
            CreateEvent(game1Id, GameEventType.Play, 2));

        // Game 2: 1 view (1 weight)
        context.Set<GameAnalyticsEventEntity>().Add(
            CreateEvent(game2Id, GameEventType.View, 0));

        // Game details
        context.Set<SharedGameEntity>().AddRange(
            new SharedGameEntity { Id = game1Id, Title = "Popular Game", ThumbnailUrl = "https://example.com/pop.jpg" },
            new SharedGameEntity { Id = game2Id, Title = "Quiet Game", ThumbnailUrl = "https://example.com/quiet.jpg" });

        await context.SaveChangesAsync();

        // Setup cache to execute factory
        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TrendingGameDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<List<TrendingGameDto>>>, string[], TimeSpan?, CancellationToken>(
                (_, factory, _, _, ct) => factory(ct));

        var handler = new GetCatalogTrendingQueryHandler(context, _cacheMock.Object, _loggerMock.Object);

        // Act
        var result = await handler.Handle(new GetCatalogTrendingQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Rank.Should().Be(1);
        result[0].GameId.Should().Be(game1Id);
        result[0].Title.Should().Be("Popular Game");
        result[0].Score.Should().BeGreaterThan(result[1].Score);
        result[1].Rank.Should().Be(2);
        result[1].GameId.Should().Be(game2Id);
    }

    [Fact]
    public async Task Handle_WithLimit_RespectsLimit()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Create 5 games with events
        for (int i = 0; i < 5; i++)
        {
            var gameId = Guid.NewGuid();
            context.Set<GameAnalyticsEventEntity>().Add(CreateEvent(gameId, GameEventType.View, 0));
            context.Set<SharedGameEntity>().Add(
                new SharedGameEntity { Id = gameId, Title = $"Game {i}" });
        }
        await context.SaveChangesAsync();

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TrendingGameDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<List<TrendingGameDto>>>, string[], TimeSpan?, CancellationToken>(
                (_, factory, _, _, ct) => factory(ct));

        var handler = new GetCatalogTrendingQueryHandler(context, _cacheMock.Object, _loggerMock.Object);

        // Act
        var result = await handler.Handle(new GetCatalogTrendingQuery(3), CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithEventCounts_ReturnsCorrectCounts()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var gameId = Guid.NewGuid();

        context.Set<GameAnalyticsEventEntity>().AddRange(
            CreateEvent(gameId, GameEventType.Search, 0),
            CreateEvent(gameId, GameEventType.Search, 1),
            CreateEvent(gameId, GameEventType.View, 0),
            CreateEvent(gameId, GameEventType.LibraryAdd, 0),
            CreateEvent(gameId, GameEventType.Play, 0),
            CreateEvent(gameId, GameEventType.Play, 1));

        context.Set<SharedGameEntity>().Add(
            new SharedGameEntity { Id = gameId, Title = "Test Game" });

        await context.SaveChangesAsync();

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TrendingGameDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<List<TrendingGameDto>>>, string[], TimeSpan?, CancellationToken>(
                (_, factory, _, _, ct) => factory(ct));

        var handler = new GetCatalogTrendingQueryHandler(context, _cacheMock.Object, _loggerMock.Object);

        // Act
        var result = await handler.Handle(new GetCatalogTrendingQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var game = result[0];
        game.SearchCount.Should().Be(2);
        game.ViewCount.Should().Be(1);
        game.LibraryAddCount.Should().Be(1);
        game.PlayCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_OldEventsExcluded_ReturnsOnlyRecentEvents()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var recentGameId = Guid.NewGuid();
        var oldGameId = Guid.NewGuid();

        // Recent event (within 7 days)
        context.Set<GameAnalyticsEventEntity>().Add(
            CreateEvent(recentGameId, GameEventType.Play, 0));

        // Old event (8+ days ago - outside window)
        context.Set<GameAnalyticsEventEntity>().Add(new GameAnalyticsEventEntity
        {
            Id = Guid.NewGuid(),
            GameId = oldGameId,
            EventType = (int)GameEventType.Play,
            Timestamp = DateTime.UtcNow.AddDays(-8)
        });

        context.Set<SharedGameEntity>().AddRange(
            new SharedGameEntity { Id = recentGameId, Title = "Recent Game" },
            new SharedGameEntity { Id = oldGameId, Title = "Old Game" });

        await context.SaveChangesAsync();

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TrendingGameDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<List<TrendingGameDto>>>, string[], TimeSpan?, CancellationToken>(
                (_, factory, _, _, ct) => factory(ct));

        var handler = new GetCatalogTrendingQueryHandler(context, _cacheMock.Object, _loggerMock.Object);

        // Act
        var result = await handler.Handle(new GetCatalogTrendingQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].GameId.Should().Be(recentGameId);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new GetCatalogTrendingQueryHandler(context, _cacheMock.Object, _loggerMock.Object);

        // Act
        var act = () => handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_CachedDataTrimmedToRequestedLimit()
    {
        // Arrange - cache has 10 items, but user requests only 3
        using var context = CreateInMemoryContext();
        var cachedTrending = Enumerable.Range(1, 10).Select(i => new TrendingGameDto
        {
            Rank = i,
            GameId = Guid.NewGuid(),
            Title = $"Game {i}",
            Score = 100.0 - i
        }).ToList();

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TrendingGameDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedTrending);

        var handler = new GetCatalogTrendingQueryHandler(context, _cacheMock.Object, _loggerMock.Object);

        // Act
        var result = await handler.Handle(new GetCatalogTrendingQuery(3), CancellationToken.None);

        // Assert - should trim to 3, not return all 10
        result.Should().HaveCount(3);
        result[0].Rank.Should().Be(1);
        result[2].Rank.Should().Be(3);
    }

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetCatalogTrendingQueryHandler(null!, _cacheMock.Object, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Act
        var act = () => new GetCatalogTrendingQueryHandler(context, null!, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    private static GameAnalyticsEventEntity CreateEvent(Guid gameId, GameEventType eventType, int daysAgo)
    {
        return new GameAnalyticsEventEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            EventType = (int)eventType,
            Timestamp = DateTime.UtcNow.AddDays(-daysAgo)
        };
    }
}
