using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for GetCacheStatsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetCacheStatsQueryHandlerTests
{
    private readonly Mock<IHybridCacheService> _mockHybridCache;
    private readonly Mock<ILogger<GetCacheStatsQueryHandler>> _mockLogger;
    private readonly GetCacheStatsQueryHandler _handler;

    public GetCacheStatsQueryHandlerTests()
    {
        _mockHybridCache = new Mock<IHybridCacheService>();
        _mockLogger = new Mock<ILogger<GetCacheStatsQueryHandler>>();
        _handler = new GetCacheStatsQueryHandler(_mockHybridCache.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithStats_ReturnsCalculatedStats()
    {
        // Arrange
        var hybridStats = new HybridCacheStats
        {
            TotalHits = 100,
            TotalMisses = 50,
            L1EntryCount = 200,
            L1MemoryBytes = 1024 * 1024 // 1 MB
        };

        _mockHybridCache.Setup(c => c.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hybridStats);

        var query = new GetCacheStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.TotalHits);
        Assert.Equal(50, result.TotalMisses);
        Assert.Equal(100.0 / 150.0, result.HitRate, 3); // 100 / 150
        Assert.Equal(200, result.TotalKeys);
        Assert.Equal(1024 * 1024, result.CacheSizeBytes);

        _mockHybridCache.Verify(c => c.GetStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoHitsOrMisses_ReturnsZeroHitRate()
    {
        // Arrange
        var hybridStats = new HybridCacheStats
        {
            TotalHits = 0,
            TotalMisses = 0,
            L1EntryCount = 0,
            L1MemoryBytes = 0
        };

        _mockHybridCache.Setup(c => c.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hybridStats);

        var query = new GetCacheStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalHits);
        Assert.Equal(0, result.TotalMisses);
        Assert.Equal(0, result.HitRate); // Avoid division by zero
        Assert.Equal(0, result.TotalKeys);
        Assert.Equal(0, result.CacheSizeBytes);
    }

    [Fact]
    public async Task Handle_WithGameIdFilter_PassesFilterToService()
    {
        // Arrange
        var gameId = "chess";
        var hybridStats = new HybridCacheStats
        {
            TotalHits = 50,
            TotalMisses = 25,
            L1EntryCount = 100,
            L1MemoryBytes = 512 * 1024
        };

        _mockHybridCache.Setup(c => c.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hybridStats);

        var query = new GetCacheStatsQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50, result.TotalHits);
        Assert.Equal(25, result.TotalMisses);
        Assert.Equal(50.0 / 75.0, result.HitRate, 3); // 50 / 75
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
