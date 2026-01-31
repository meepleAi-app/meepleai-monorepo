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
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Issue #880: Unit tests for AdminStatsService to achieve 90%+ coverage.
/// Supplements integration tests by covering:
/// - Role filter branches in GetUserTrendAsync
/// - Game filter branches in GetApiRequestTrendAsync/GetPdfUploadTrendAsync
/// - Invalid export format exception
/// - FillMissingDates edge cases
/// - Constructor variations
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AdminStatsServiceUnitTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly FakeHybridCache _cache;
    private readonly Mock<ILogger<AdminStatsService>> _mockLogger;
    private readonly FakeTimeProvider _timeProvider;
    private readonly AdminStatsService _service;

    public AdminStatsServiceUnitTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"AdminStatsUnitTestDb_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        _cache = new FakeHybridCache();
        _mockLogger = new Mock<ILogger<AdminStatsService>>();

        // Use FakeTimeProvider for deterministic time control (Issue #880)
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero));

        _service = new AdminStatsService(
            _dbContext,
            _cache,
            _mockLogger.Object,
            _timeProvider);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbContext?.Dispose();
        }
    }

    #region Role Filter Tests

    /// <summary>
    /// Issue #880: Test role filter with specific role (admin)
    /// Covers branch: roleFilter != "all" && !string.IsNullOrWhiteSpace(roleFilter)
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_WithAdminRoleFilter_FiltersUsersByRole()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await SeedUsersWithRolesAsync(now, adminCount: 2, userCount: 8);

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-30),
            ToDate: now,
            Days: 30,
            RoleFilter: "admin"
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        // UserTrend should only count admin users
        var totalFromTrend = result.UserTrend.Sum(t => t.Count);
        totalFromTrend.Should().Be(2, "only admin users should be counted in trend");
    }

    /// <summary>
    /// Issue #880: Test role filter with "user" role
    /// Covers branch: roleFilter == "user"
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_WithUserRoleFilter_FiltersUsersByRole()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await SeedUsersWithRolesAsync(now, adminCount: 2, userCount: 8);

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-30),
            ToDate: now,
            Days: 30,
            RoleFilter: "user"
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        var totalFromTrend = result.UserTrend.Sum(t => t.Count);
        totalFromTrend.Should().Be(8, "only regular users should be counted in trend");
    }

    /// <summary>
    /// Issue #880: Test role filter with "all" value
    /// Covers branch: roleFilter == "all" (no filtering applied)
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_WithAllRoleFilter_ReturnsAllUsers()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await SeedUsersWithRolesAsync(now, adminCount: 2, userCount: 8);

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-30),
            ToDate: now,
            Days: 30,
            RoleFilter: "all"
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        var totalFromTrend = result.UserTrend.Sum(t => t.Count);
        totalFromTrend.Should().Be(10, "all users should be counted when filter is 'all'");
    }

    /// <summary>
    /// Issue #880: Test role filter with null/empty value
    /// Covers branch: string.IsNullOrWhiteSpace(roleFilter)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetDashboardStatsAsync_WithEmptyRoleFilter_ReturnsAllUsers(string? roleFilter)
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await SeedUsersWithRolesAsync(now, adminCount: 2, userCount: 8);

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-30),
            ToDate: now,
            Days: 30,
            RoleFilter: roleFilter
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        var totalFromTrend = result.UserTrend.Sum(t => t.Count);
        totalFromTrend.Should().Be(10, "all users should be counted when role filter is null/empty");
    }

    #endregion

    #region Game Filter Tests

    /// <summary>
    /// Issue #880: Test API request trend with game filter
    /// Covers branch: !string.IsNullOrWhiteSpace(gameId) && Guid.TryParse
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_WithGameIdFilter_FiltersApiRequestsByGame()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var (game1Id, _) = await SeedDataWithMultipleGamesAsync(now);

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-30),
            ToDate: now,
            Days: 30,
            GameId: game1Id.ToString()
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        // ApiRequestTrend should only count requests for game1
        var totalApiRequests = result.ApiRequestTrend.Sum(t => t.Count);
        totalApiRequests.Should().Be(5, "only game1 requests (5) should be counted");
    }

    /// <summary>
    /// Issue #880: Test PDF upload trend with game filter
    /// Covers branch: gameId filter in GetPdfUploadTrendAsync
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_WithGameIdFilter_FiltersPdfsByGame()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var (game1Id, _) = await SeedDataWithMultipleGamesAsync(now);

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-30),
            ToDate: now,
            Days: 30,
            GameId: game1Id.ToString()
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        var totalPdfs = result.PdfUploadTrend.Sum(t => t.Count);
        totalPdfs.Should().Be(3, "only game1 PDFs (3) should be counted");
    }

    /// <summary>
    /// Issue #880: Test with invalid game ID (non-GUID string)
    /// Covers branch: Guid.TryParse returns false
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_WithInvalidGameId_ReturnsAllRequests()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await SeedDataWithMultipleGamesAsync(now);

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-30),
            ToDate: now,
            Days: 30,
            GameId: "invalid-not-a-guid"
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        // Should return all requests since gameId is invalid
        var totalApiRequests = result.ApiRequestTrend.Sum(t => t.Count);
        totalApiRequests.Should().Be(8, "all requests (5+3) should be counted with invalid gameId");
    }

    #endregion

    #region Export Format Tests

    /// <summary>
    /// Issue #880: Test export with unsupported format throws ArgumentException
    /// Covers branch: switch default case
    /// </summary>
    [Theory]
    [InlineData("xml")]
    [InlineData("pdf")]
    [InlineData("excel")]
    [InlineData("UNKNOWN")]
    public async Task ExportDashboardDataAsync_UnsupportedFormat_ThrowsArgumentException(string format)
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var request = new ExportDataRequest(
            Format: format,
            FromDate: now.AddDays(-7),
            ToDate: now
        );

        // Act
        var act = () => _service.ExportDashboardDataAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*Unsupported export format: {format}*");
    }

    /// <summary>
    /// Issue #880: Test export format is case-insensitive
    /// Covers: format.ToLowerInvariant() normalization
    /// </summary>
    [Theory]
    [InlineData("CSV")]
    [InlineData("Csv")]
    [InlineData("JSON")]
    [InlineData("Json")]
    public async Task ExportDashboardDataAsync_CaseInsensitiveFormat_ReturnsData(string format)
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var request = new ExportDataRequest(
            Format: format,
            FromDate: now.AddDays(-7),
            ToDate: now
        );

        // Act
        var result = await _service.ExportDashboardDataAsync(request);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region FillMissingDates Tests

    /// <summary>
    /// Issue #880: Test FillMissingDates fills gaps in date range
    /// Covers: Loop adding zero-count entries for missing dates
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_SparseData_FillsMissingDates()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Seed data only for specific dates, leaving gaps
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "sparse@test.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = now.AddDays(-5) // Only one user 5 days ago
        };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-7),
            ToDate: now,
            Days: 7
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        result.UserTrend.Should().HaveCount(8, "should have 8 days (day -7 to day 0)");

        // Verify only one date has count > 0
        var daysWithUsers = result.UserTrend.Count(t => t.Count > 0);
        daysWithUsers.Should().Be(1, "only one day should have users");

        // Verify missing dates are filled with 0
        var daysWithZero = result.UserTrend.Count(t => t.Count == 0);
        daysWithZero.Should().Be(7, "7 days should have zero count");
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Issue #880: Test constructor with null TimeProvider uses System default
    /// Covers: _timeProvider = timeProvider ?? TimeProvider.System
    /// </summary>
    [Fact]
    public async Task Constructor_WithNullTimeProvider_UsesSystemTimeProvider()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"ConstructorTestDb_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
        using var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);

        var cache = new FakeHybridCache();
        var logger = new Mock<ILogger<AdminStatsService>>();

        // Act - Create service with null TimeProvider
        var service = new AdminStatsService(dbContext, cache, logger.Object, null);

        var queryParams = new AnalyticsQueryParams(Days: 1);
        var result = await service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// Issue #880: Test with explicit date range overriding Days parameter
    /// Covers: FromDate/ToDate taking precedence over Days
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_ExplicitDateRange_OverridesDays()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Seed user in specific date range
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "daterange@test.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = now.AddDays(-3)
        };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        // Query with explicit 5-day range (should include user) but Days=1 (would exclude user)
        var queryParams = new AnalyticsQueryParams(
            FromDate: now.AddDays(-5),
            ToDate: now,
            Days: 1 // This should be overridden by explicit dates
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        var totalFromTrend = result.UserTrend.Sum(t => t.Count);
        totalFromTrend.Should().Be(1, "user should be included in explicit date range");
        result.UserTrend.Should().HaveCount(6, "should cover 6 days from -5 to 0");
    }

    /// <summary>
    /// Issue #880: Test null confidence values are handled correctly
    /// Covers: log.Confidence.HasValue filter in average calculation
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_NullConfidenceValues_AveragesOnlyValidValues()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "confidence@test.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = now
        };
        await _dbContext.Users.AddAsync(user);

        // Create requests: 2 with confidence, 2 without
        var requests = new[]
        {
            new AiRequestLogEntity { Id = Guid.NewGuid(), UserId = user.Id, Endpoint = "/chat", Status = "Success", LatencyMs = 100, TokenCount = 50, Confidence = 0.80, CreatedAt = now },
            new AiRequestLogEntity { Id = Guid.NewGuid(), UserId = user.Id, Endpoint = "/chat", Status = "Success", LatencyMs = 100, TokenCount = 50, Confidence = 0.90, CreatedAt = now },
            new AiRequestLogEntity { Id = Guid.NewGuid(), UserId = user.Id, Endpoint = "/chat", Status = "Success", LatencyMs = 100, TokenCount = 50, Confidence = null, CreatedAt = now },
            new AiRequestLogEntity { Id = Guid.NewGuid(), UserId = user.Id, Endpoint = "/chat", Status = "Success", LatencyMs = 100, TokenCount = 50, Confidence = null, CreatedAt = now }
        };
        await _dbContext.AiRequestLogs.AddRangeAsync(requests);
        await _dbContext.SaveChangesAsync();

        var queryParams = new AnalyticsQueryParams(Days: 1);

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        result.Metrics.AverageConfidenceScore.Should().BeApproximately(0.85, 0.001,
            "average should be (0.80 + 0.90) / 2 = 0.85, ignoring null values");
    }

    /// <summary>
    /// Issue #880: Test null latency values return 0 average
    /// Covers: AverageAsync returning null when no valid values
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_NoLatencyData_ReturnsZeroAverage()
    {
        // Arrange - No AI requests seeded
        var queryParams = new AnalyticsQueryParams(Days: 1);

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        result.Metrics.AverageLatency24h.Should().Be(0.0, "no requests should result in 0 average latency");
        result.Metrics.AverageLatency7d.Should().Be(0.0);
    }

    #endregion

    #region Time Series AverageValue Tests

    /// <summary>
    /// Issue #880: Test ApiRequestTrend includes average confidence
    /// Covers: AvgConfidence projection in GroupBy
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_ApiRequestTrend_IncludesAverageConfidence()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "trend@test.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = now
        };
        await _dbContext.Users.AddAsync(user);

        // Create requests with known confidence values
        var requests = new[]
        {
            new AiRequestLogEntity { Id = Guid.NewGuid(), UserId = user.Id, Endpoint = "/chat", Status = "Success", LatencyMs = 100, TokenCount = 50, Confidence = 0.70, CreatedAt = now },
            new AiRequestLogEntity { Id = Guid.NewGuid(), UserId = user.Id, Endpoint = "/chat", Status = "Success", LatencyMs = 100, TokenCount = 50, Confidence = 0.80, CreatedAt = now },
        };
        await _dbContext.AiRequestLogs.AddRangeAsync(requests);
        await _dbContext.SaveChangesAsync();

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.Date,
            ToDate: now,
            Days: 1
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        var todayData = result.ApiRequestTrend.FirstOrDefault(t => t.Date.Date == now.Date);
        todayData.Should().NotBeNull();
        todayData!.AverageValue.Should().BeApproximately(0.75, 0.001, "average confidence should be (0.70 + 0.80) / 2");
    }

    /// <summary>
    /// Issue #880: Test PdfUploadTrend includes average page count
    /// Covers: AvgPages projection in GroupBy
    /// </summary>
    [Fact]
    public async Task GetDashboardStatsAsync_PdfUploadTrend_IncludesAveragePages()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "pdf@test.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = now
        };
        await _dbContext.Users.AddAsync(user);

        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Game",
            CreatedAt = now
        };
        await _dbContext.Games.AddAsync(game);

        // Create PDFs with known page counts
        var pdfs = new[]
        {
            new PdfDocumentEntity { Id = Guid.NewGuid(), GameId = game.Id, FileName = "a.pdf", FilePath = "a.pdf", FileSizeBytes = 1000, UploadedByUserId = user.Id, PageCount = 10, UploadedAt = now },
            new PdfDocumentEntity { Id = Guid.NewGuid(), GameId = game.Id, FileName = "b.pdf", FilePath = "b.pdf", FileSizeBytes = 1000, UploadedByUserId = user.Id, PageCount = 30, UploadedAt = now },
        };
        await _dbContext.PdfDocuments.AddRangeAsync(pdfs);
        await _dbContext.SaveChangesAsync();

        var queryParams = new AnalyticsQueryParams(
            FromDate: now.Date,
            ToDate: now,
            Days: 1
        );

        // Act
        var result = await _service.GetDashboardStatsAsync(queryParams);

        // Assert
        result.Should().NotBeNull();
        var todayData = result.PdfUploadTrend.FirstOrDefault(t => t.Date.Date == now.Date);
        todayData.Should().NotBeNull();
        todayData!.AverageValue.Should().BeApproximately(20.0, 0.001, "average pages should be (10 + 30) / 2");
    }

    #endregion

    #region Helper Methods

    private async Task SeedUsersWithRolesAsync(DateTime now, int adminCount, int userCount)
    {
        var users = new List<UserEntity>();

        for (int i = 0; i < adminCount; i++)
        {
            users.Add(new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = $"admin{i}@test.com",
                PasswordHash = "hash",
                Role = "admin",
                CreatedAt = now.AddDays(-i - 1)
            });
        }

        for (int i = 0; i < userCount; i++)
        {
            users.Add(new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = $"user{i}@test.com",
                PasswordHash = "hash",
                Role = "user",
                CreatedAt = now.AddDays(-i - 1)
            });
        }

        await _dbContext.Users.AddRangeAsync(users);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<(Guid Game1Id, Guid Game2Id)> SeedDataWithMultipleGamesAsync(DateTime now)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "gametest@test.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = now.AddDays(-5)
        };
        await _dbContext.Users.AddAsync(user);

        var game1 = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Game 1",
            CreatedAt = now.AddDays(-10)
        };
        var game2 = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Game 2",
            CreatedAt = now.AddDays(-10)
        };
        await _dbContext.Games.AddRangeAsync(game1, game2);

        // 5 requests for game1, 3 for game2
        var requests = new List<AiRequestLogEntity>();
        for (int i = 0; i < 5; i++)
        {
            requests.Add(new AiRequestLogEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                GameId = game1.Id,
                Endpoint = "/chat",
                Status = "Success",
                LatencyMs = 100,
                TokenCount = 50,
                CreatedAt = now.AddDays(-i)
            });
        }
        for (int i = 0; i < 3; i++)
        {
            requests.Add(new AiRequestLogEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                GameId = game2.Id,
                Endpoint = "/chat",
                Status = "Success",
                LatencyMs = 100,
                TokenCount = 50,
                CreatedAt = now.AddDays(-i)
            });
        }
        await _dbContext.AiRequestLogs.AddRangeAsync(requests);

        // 3 PDFs for game1, 2 for game2
        var pdfs = new List<PdfDocumentEntity>();
        for (int i = 0; i < 3; i++)
        {
            pdfs.Add(new PdfDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = game1.Id,
                FileName = $"game1_{i}.pdf",
                FilePath = $"path/game1_{i}.pdf",
                FileSizeBytes = 1000,
                UploadedByUserId = user.Id,
                PageCount = 10,
                UploadedAt = now.AddDays(-i)
            });
        }
        for (int i = 0; i < 2; i++)
        {
            pdfs.Add(new PdfDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = game2.Id,
                FileName = $"game2_{i}.pdf",
                FilePath = $"path/game2_{i}.pdf",
                FileSizeBytes = 1000,
                UploadedByUserId = user.Id,
                PageCount = 10,
                UploadedAt = now.AddDays(-i)
            });
        }
        await _dbContext.PdfDocuments.AddRangeAsync(pdfs);

        await _dbContext.SaveChangesAsync();

        return (game1.Id, game2.Id);
    }

    #endregion
}