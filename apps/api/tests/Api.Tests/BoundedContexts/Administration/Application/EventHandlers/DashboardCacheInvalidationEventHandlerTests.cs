using Api.BoundedContexts.Administration.Application.EventHandlers;
using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Issue #879: Unit tests for DashboardCacheInvalidationEventHandler.
/// Verifies cache invalidation on configuration update and toggle events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DashboardCacheInvalidationEventHandlerTests
{
    private readonly Mock<HybridCache> _cacheMock;
    private readonly Mock<ILogger<DashboardCacheInvalidationEventHandler>> _loggerMock;
    private readonly DashboardCacheInvalidationEventHandler _handler;

    public DashboardCacheInvalidationEventHandlerTests()
    {
        _cacheMock = new Mock<HybridCache>();
        _loggerMock = new Mock<ILogger<DashboardCacheInvalidationEventHandler>>();
        _handler = new DashboardCacheInvalidationEventHandler(_cacheMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ConfigurationUpdatedEvent_InvalidatesDashboardCache()
    {
        // Arrange
        var configKey = new ConfigKey("Features.StreamingResponses");
        var evt = new ConfigurationUpdatedEvent(
            configurationId: Guid.NewGuid(),
            key: configKey,
            previousValue: "false",
            newValue: "true",
            version: 2,
            updatedByUserId: Guid.NewGuid());

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — #4542: one tag covers every entry, whatever its date range or filters
        _cacheMock.Verify(
            c => c.RemoveByTagAsync(AdminStatsService.DashboardStatsCacheTag, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should invalidate every dashboard stats entry by its tag");
    }

    [Fact]
    public async Task Handle_ConfigurationToggledEvent_InvalidatesDashboardCache()
    {
        // Arrange
        var configKey = new ConfigKey("RateLimit.Api.Requests");
        var evt = new ConfigurationToggledEvent(
            configurationId: Guid.NewGuid(),
            key: configKey,
            isActive: true,
            toggledByUserId: Guid.NewGuid());

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync(AdminStatsService.DashboardStatsCacheTag, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should invalidate dashboard cache on configuration toggle");
    }

    [Fact]
    public async Task Handle_ConfigurationUpdatedEvent_LogsInvalidation()
    {
        // Arrange
        var configKey = new ConfigKey("TestConfig");
        var evt = new ConfigurationUpdatedEvent(
            configurationId: Guid.NewGuid(),
            key: configKey,
            previousValue: "old",
            newValue: "new",
            version: 1,
            updatedByUserId: Guid.NewGuid());

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Configuration updated")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Dashboard cache invalidated")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CacheRemovalThrows_LogsErrorButDoesNotThrow()
    {
        // Arrange
        var configKey = new ConfigKey("TestConfig");
        var evt = new ConfigurationUpdatedEvent(
            configurationId: Guid.NewGuid(),
            key: configKey,
            previousValue: "old",
            newValue: "new",
            version: 1,
            updatedByUserId: Guid.NewGuid());

        _cacheMock
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache error"));

        // Act
        var exception = await Record.ExceptionAsync(() => _handler.Handle(evt, CancellationToken.None));

        // Assert
        exception.Should().BeNull(); // Should not throw
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate dashboard cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log error when cache removal fails");
    }

    /// <summary>
    /// #4542: l'invalidazione deve raggiungere le voci che <see cref="AdminStatsService"/> scrive davvero,
    /// qualunque sia il formato della chiave. Cache vera (L1 + L2 in memoria), servizio vero: tre voci —
    /// intervallo di default, intervallo esplicito, filtro per gioco — devono essere ricalcolate dopo un
    /// cambio di configurazione. <c>generatedAt</c> viene dall'orologio finto, quindi dice se la risposta
    /// è stata ricalcolata o servita dalla cache.
    /// </summary>
    [Fact]
    public async Task Handle_ConfigurationUpdated_EvictsEveryDashboardStatsEntry()
    {
        var cache = CreateHybridCache();
        var clock = new FakeTimeProvider(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var resourceMetrics = new Mock<IResourceMetricsService>();
        resourceMetrics.Setup(m => m.GetTokenBalanceAsync(It.IsAny<CancellationToken>())).ReturnsAsync((0m, 0m));
        resourceMetrics.Setup(m => m.GetDatabaseMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((0m, 0m, 0m));
        resourceMetrics.Setup(m => m.GetCacheHitRateAsync(It.IsAny<CancellationToken>())).ReturnsAsync((0.0, 0.0));
        var service = new AdminStatsService(
            db, cache, Mock.Of<ILogger<AdminStatsService>>(), resourceMetrics.Object, clock);
        var handler = new DashboardCacheInvalidationEventHandler(cache, _loggerMock.Object);

        AnalyticsQueryParams[] requests =
        [
            new(Days: 30),
            new(FromDate: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), ToDate: new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc)),
            new(Days: 30, GameId: Guid.NewGuid().ToString()),
        ];

        var computedAt = clock.GetUtcNow().UtcDateTime;
        foreach (var request in requests)
        {
            (await service.GetDashboardStatsAsync(request)).GeneratedAt.Should().Be(computedAt);
        }

        clock.Advance(TimeSpan.FromSeconds(1));
        foreach (var request in requests)
        {
            (await service.GetDashboardStatsAsync(request)).GeneratedAt.Should().Be(computedAt,
                "prima dell'evento la voce è in cache: è il controllo che rende significativo il passo dopo");
        }

        await handler.Handle(new ConfigurationUpdatedEvent(
            configurationId: Guid.NewGuid(),
            key: new ConfigKey("TestConfig"),
            previousValue: "old",
            newValue: "new",
            version: 1,
            updatedByUserId: Guid.NewGuid()), CancellationToken.None);

        foreach (var request in requests)
        {
            (await service.GetDashboardStatsAsync(request)).GeneratedAt.Should().Be(clock.GetUtcNow().UtcDateTime,
                $"dopo il cambio di configurazione la voce {request} deve essere ricalcolata");
        }
    }

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
