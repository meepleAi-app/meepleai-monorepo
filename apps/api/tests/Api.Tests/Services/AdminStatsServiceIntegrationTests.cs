using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Fake HybridCache implementation with in-memory storage for testing cache behavior.
/// Code Review Fix (Issue #876): Tests actual cache hits/misses instead of always bypassing.
/// </summary>
internal class FakeHybridCache : HybridCache
{
    private readonly Dictionary<string, (object Value, DateTime Expiry, HashSet<string> Tags)> _cache = new();
    private readonly object _lock = new();

    public int HitCount { get; private set; }
    public int MissCount { get; private set; }

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // Check for cache hit
            if (_cache.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow)
            {
                HitCount++;
                return (T)entry.Value;
            }

            MissCount++;
        }

        // Cache miss - call factory
        var value = await factory(state, cancellationToken);

        // Store in cache
        var expiry = DateTime.UtcNow.Add(options?.Expiration ?? TimeSpan.FromMinutes(5));
        var cacheTags = tags != null ? new HashSet<string>(tags) : new HashSet<string>();

        lock (_lock)
        {
            _cache[key] = (value!, expiry, cacheTags);
        }

        return value;
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var expiry = DateTime.UtcNow.Add(options?.Expiration ?? TimeSpan.FromMinutes(5));
        var cacheTags = tags != null ? new HashSet<string>(tags) : new HashSet<string>();

        lock (_lock)
        {
            _cache[key] = (value!, expiry, cacheTags);
        }

        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _cache.Remove(key);
        }

        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveByTagAsync(
        string tag,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var keysToRemove = _cache
                .Where(kvp => kvp.Value.Tags.Contains(tag))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Test helper: Clear all cache entries and reset counters.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            HitCount = 0;
            MissCount = 0;
        }
    }
}

/// <summary>
/// Integration tests for AdminStatsService.
/// Issue #876: Validate parallel aggregation, cache behavior, derived metrics, and performance.
///
/// Coverage:
/// - Parallel aggregation logic (Task.WhenAll)
/// - Cache hit/miss scenarios (using FakeHybridCache with in-memory storage)
/// - Derived metrics calculation (ErrorRate with edge cases via Theory)
/// - Export functionality (CSV + JSON)
/// - Performance validation
///
/// Code Review Improvements:
/// - FakeHybridCache: In-memory cache implementation with hit/miss tracking
/// - TimeProvider: Consistent usage for deterministic test data
/// - Error Rate Theory: Parameterized testing for robustness (0%, 1%, 20%, 100%)
/// - Documentation: InMemory DB rationale (Docker hijack prevention #895)
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class AdminStatsServiceIntegrationTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly FakeHybridCache _cache;
    private readonly Mock<ILogger<AdminStatsService>> _mockLogger;
    private readonly Mock<IResourceMetricsService> _mockResourceMetrics;
    private readonly AdminStatsService _service;
    private readonly TimeProvider _timeProvider;

    public AdminStatsServiceIntegrationTests()
    {
        // Use in-memory database for fast integration tests (avoids Docker hijack issues #895)
        // AdminStatsService queries are read-only aggregations, in-memory DB sufficient for testing
        // Note: Parallel aggregation logic and cache behavior are architecture-independent
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        _cache = new FakeHybridCache();
        _mockLogger = new Mock<ILogger<AdminStatsService>>();
        // Use System TimeProvider (consider FakeTimeProvider for deterministic time-based tests)
        _timeProvider = TimeProvider.System;

        // Issue #3694: Mock IResourceMetricsService for extended KPIs
        _mockResourceMetrics = new Mock<IResourceMetricsService>();
        _mockResourceMetrics.Setup(m => m.GetTokenBalanceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((450m, 1000m));
        _mockResourceMetrics.Setup(m => m.GetDatabaseMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((2.3m, 10m, 50m));
        _mockResourceMetrics.Setup(m => m.GetCacheHitRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((94.2, 2.1));

        _service = new AdminStatsService(
            _dbContext,
            _cache,
            _mockLogger.Object,
            _mockResourceMetrics.Object,
            _timeProvider);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    /// <summary>
    /// Test01: Verify GetDashboardStatsAsync performs parallel aggregation correctly
    /// Issue #876: Task.WhenAll for all 6 trend queries
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task GetDashboardStatsAsync_ParallelAggregation_ReturnsAllMetrics()
    {
        // Arrange
        await SeedTestDataAsync();
        // FakeHybridCache always bypasses cache, so no setup needed

        var queryParams = new AnalyticsQueryParams(
            FromDate: DateTime.UtcNow.AddDays(-30),
            ToDate: DateTime.UtcNow,
            Days: 30);

        // Act
        var sw = Stopwatch.StartNew();
        var result = await _service.GetDashboardStatsAsync(queryParams, TestContext.Current.CancellationToken);
        sw.Stop();

        // Assert - All metrics populated (parallel aggregation successful)
        result.Should().NotBeNull();
        result.Metrics.Should().NotBeNull();
        result.Metrics.TotalUsers.Should().Be(10); // Seeded users
        result.Metrics.ActiveSessions.Should().BeGreaterThan(0);
        result.Metrics.TotalPdfDocuments.Should().BeGreaterThan(0);
        result.Metrics.TotalChatMessages.Should().BeGreaterThan(0);
        result.Metrics.TotalRagRequests.Should().BeGreaterThan(0);

        // Issue #874: New metrics validated
        result.Metrics.TotalGames.Should().BeGreaterThan(0);
        result.Metrics.ApiRequests7d.Should().BeGreaterThan(0);
        result.Metrics.ApiRequests30d.Should().BeGreaterThan(0);
        result.Metrics.AverageLatency24h.Should().BeGreaterThan(0);
        result.Metrics.ActiveAlerts.Should().Be(2); // Seeded active alerts

        // Time series trends populated
        result.UserTrend.Should().NotBeEmpty();
        result.SessionTrend.Should().NotBeEmpty();
        result.ApiRequestTrend.Should().NotBeEmpty();
        result.PdfUploadTrend.Should().NotBeEmpty();
        result.ChatMessageTrend.Should().NotBeEmpty();

        // Performance validation (cache miss expected ~2-3s for in-memory DB)
        sw.ElapsedMilliseconds.Should().BeLessThan(5000, "parallel aggregation should complete within 5s");
    }

    /// <summary>
    /// Test02: Verify cache hit scenario returns cached data
    /// Issue #876 + Code Review: FakeHybridCache now properly caches values
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task GetDashboardStatsAsync_CacheHit_ReturnsCachedData()
    {
        // Arrange
        await SeedTestDataAsync();
        var queryParams = new AnalyticsQueryParams(Days: 30);

        // Act - First call (cache miss)
        var result1 = await _service.GetDashboardStatsAsync(queryParams, TestContext.Current.CancellationToken);
        var missCount1 = _cache.MissCount;

        // Act - Second call with same params (cache hit)
        var result2 = await _service.GetDashboardStatsAsync(queryParams, TestContext.Current.CancellationToken);
        var hitCount2 = _cache.HitCount;

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        missCount1.Should().Be(1, "first call should be cache miss");
        hitCount2.Should().Be(1, "second call should be cache hit");
        result1.Metrics.TotalUsers.Should().Be(result2.Metrics.TotalUsers, "cached data should match");
    }

    /// <summary>
    /// Test03: Verify cache miss triggers aggregation
    /// Issue #876 + Code Review: FakeHybridCache tracks miss count
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task GetDashboardStatsAsync_CacheMiss_ExecutesAggregation()
    {
        // Arrange
        await SeedTestDataAsync();
        _cache.Clear(); // Ensure empty cache
        var queryParams = new AnalyticsQueryParams(Days: 7);

        // Act
        var initialMissCount = _cache.MissCount;
        var result = await _service.GetDashboardStatsAsync(queryParams, TestContext.Current.CancellationToken);
        var finalMissCount = _cache.MissCount;

        // Assert
        result.Should().NotBeNull();
        result.Metrics.TotalUsers.Should().Be(10);
        (finalMissCount - initialMissCount).Should().Be(1, "should register one cache miss");
    }

    /// <summary>
    /// Test04: Verify derived metric calculation (ErrorRate) with multiple scenarios
    /// Issue #876: Calculate derived metrics - ErrorRate = errorCount / totalCount
    /// Code Review Fix: Use Theory for robust testing of edge cases
    /// </summary>
    [Theory(Timeout = 30000)]
    [InlineData(0, 10, 0.0, "No errors")]
    [InlineData(2, 10, 0.20, "20% error rate")]
    [InlineData(10, 10, 1.0, "100% errors")]
    [InlineData(1, 100, 0.01, "1% error rate")]
    public async Task GetDashboardStatsAsync_DerivedMetrics_CalculatesErrorRateCorrectly(
        int errorCount, int totalCount, double expectedRate, string scenario)
    {
        // Arrange
        await SeedTestDataWithSpecificErrors(errorCount, totalCount);

        var queryParams = new AnalyticsQueryParams(Days: 1); // Last 24h

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull($"scenario: {scenario}");
        result.Metrics.ErrorRate24h.Should().BeApproximately(expectedRate, 0.001,
            $"error rate should be {errorCount}/{totalCount} = {expectedRate} ({scenario})");
    }

    /// <summary>
    /// Test05: Verify zero-division safety in derived metrics
    /// Issue #876: ErrorRate calculation should handle zero totalCount gracefully
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task GetDashboardStatsAsync_NoRequests_ErrorRateIsZero()
    {
        // Arrange - Empty database (no AI requests)
        _cache.Clear();

        var queryParams = new AnalyticsQueryParams(Days: 1);

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Metrics.ErrorRate24h.Should().Be(0.0, "error rate should be 0 when no requests exist");
    }

    /// <summary>
    /// Test06: Verify ExportDashboardDataAsync CSV format
    /// Issue #877: Export functionality validates aggregated data
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task ExportDashboardDataAsync_CsvFormat_ReturnsValidCsvData()
    {
        // Arrange
        await SeedTestDataAsync();
        var request = new ExportDataRequest(
            Format: "csv",
            FromDate: _timeProvider.GetUtcNow().UtcDateTime.AddDays(-7),
            ToDate: _timeProvider.GetUtcNow().UtcDateTime
        );

        // Act
        var result = await _service.ExportDashboardDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Metric,Value", "CSV should have header row");
        result.Should().Contain("Total Users,", "CSV should include user metrics");
        result.Should().Contain("User Registrations - Date,Count,Average", "CSV should include time series");
    }

    /// <summary>
    /// Test07: Verify ExportDashboardDataAsync JSON format
    /// Issue #877: Export functionality validates JSON serialization
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task ExportDashboardDataAsync_JsonFormat_ReturnsValidJsonData()
    {
        // Arrange
        await SeedTestDataAsync();
        var request = new ExportDataRequest(
            Format: "json",
            FromDate: _timeProvider.GetUtcNow().UtcDateTime.AddDays(-7),
            ToDate: _timeProvider.GetUtcNow().UtcDateTime
        );

        // Act
        var result = await _service.ExportDashboardDataAsync(request, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith("{", "JSON should start with opening brace");
        result.Should().Contain("\"metrics\":", "JSON should include metrics object");
        result.Should().Contain("\"totalUsers\":", "JSON should include user metrics");
    }

    // === Helper Methods ===

    private async Task SeedTestDataAsync()
    {
        // Use TimeProvider for deterministic test data (Issue #876 code review)
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var startOfDay = now.Date;

        // Seed 10 users
        var users = Enumerable.Range(1, 10).Select(i => new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"user{i}@test.com",
            PasswordHash = "hash",
            Role = i <= 2 ? "admin" : "user",
            CreatedAt = now.AddDays(-i)
        }).ToList();
        await _dbContext.Users.AddRangeAsync(users);

        // Seed 5 active sessions
        var sessions = users.Take(5).Select(u => new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = u.Id,
            TokenHash = Guid.NewGuid().ToString(),
            User = u,
            ExpiresAt = now.AddHours(1),
            CreatedAt = now.AddHours(-2)
        }).ToList();
        await _dbContext.UserSessions.AddRangeAsync(sessions);

        // Seed 3 games
        var games = Enumerable.Range(1, 3).Select(i => new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = $"Game {i}",
            CreatedAt = now.AddDays(-i)
        }).ToList();
        await _dbContext.Games.AddRangeAsync(games);

        // Seed 10 AI request logs (8 success, 2 errors)
        var aiRequests = Enumerable.Range(1, 10).Select(i => new AiRequestLogEntity
        {
            Id = Guid.NewGuid(),
            UserId = users[0].Id,
            GameId = games[0].Id,
            Endpoint = "/api/v1/chat",
            Status = i <= 8 ? "Success" : "Error",
            LatencyMs = 150 + (i * 10),
            TokenCount = 100,
            Confidence = 0.85,
            CreatedAt = startOfDay.AddHours(i)
        }).ToList();
        await _dbContext.AiRequestLogs.AddRangeAsync(aiRequests);

        // Seed 5 PDF documents
        var pdfs = Enumerable.Range(1, 5).Select(i => new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = games[0].Id,
            FileName = $"rulebook{i}.pdf",
            FilePath = $"path/rulebook{i}.pdf",
            FileSizeBytes = 1024 * 1024,
            UploadedByUserId = users[0].Id,
            PageCount = 20,
            UploadedAt = now.AddDays(-i)
        }).ToList();
        await _dbContext.PdfDocuments.AddRangeAsync(pdfs);

        // Seed 15 chat messages (agent system removed — use random Guid for AgentId, FK dropped in migration)
        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = users[0].Id,
            GameId = games[0].Id,
            AgentId = Guid.NewGuid(),
            StartedAt = now.AddDays(-1)
        };
        await _dbContext.Chats.AddAsync(chat);

        var chatLogs = Enumerable.Range(1, 15).Select(i => new ChatLogEntity
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            UserId = users[0].Id,
            Level = i % 2 == 0 ? "user" : "assistant",
            Message = $"Message {i}",
            SequenceNumber = i,
            CreatedAt = now.AddHours(-i)
        }).ToList();
        await _dbContext.ChatLogs.AddRangeAsync(chatLogs);

        // Seed 2 active alerts + 3 resolved alerts
        var alerts = new List<AlertEntity>
        {
            new AlertEntity { Id = Guid.NewGuid(), AlertType = "HighErrorRate", Severity = "critical", Message = "Alert 1", IsActive = true, TriggeredAt = now.AddHours(-1) },
            new AlertEntity { Id = Guid.NewGuid(), AlertType = "DatabaseSlow", Severity = "warning", Message = "Alert 2", IsActive = true, TriggeredAt = now.AddHours(-2) },
            new AlertEntity { Id = Guid.NewGuid(), AlertType = "MinorIssue", Severity = "info", Message = "Alert 3", IsActive = false, ResolvedAt = now.AddHours(-3), TriggeredAt = now.AddDays(-1) },
            new AlertEntity { Id = Guid.NewGuid(), AlertType = "MinorIssue", Severity = "info", Message = "Alert 4", IsActive = false, ResolvedAt = now.AddDays(-1), TriggeredAt = now.AddDays(-2) },
            new AlertEntity { Id = Guid.NewGuid(), AlertType = "MinorIssue", Severity = "info", Message = "Alert 5", IsActive = false, ResolvedAt = now.AddDays(-2), TriggeredAt = now.AddDays(-3) }
        };
        await _dbContext.Alerts.AddRangeAsync(alerts);

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seed test data with specific error count for Theory-based error rate testing.
    /// Code Review Fix: Parameterized seeding for robust test scenarios.
    /// </summary>
    private async Task SeedTestDataWithSpecificErrors(int errorCount, int totalCount)
    {
        // Use TimeProvider for deterministic test data (Issue #876 code review)
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var startOfDay = now.Date;

        // Seed 1 user
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = now.AddDays(-1)
        };
        await _dbContext.Users.AddAsync(user);

        // Seed AI requests with specified error count
        var successCount = totalCount - errorCount;
        var requests = Enumerable.Range(1, totalCount).Select(i => new AiRequestLogEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Endpoint = "/api/v1/chat",
            Status = i <= successCount ? "Success" : "Error",
            LatencyMs = 200,
            TokenCount = 50,
            CreatedAt = startOfDay.AddHours(i % 24) // Distribute across 24h
        }).ToList();
        await _dbContext.AiRequestLogs.AddRangeAsync(requests);

        await _dbContext.SaveChangesAsync();
    }

}
