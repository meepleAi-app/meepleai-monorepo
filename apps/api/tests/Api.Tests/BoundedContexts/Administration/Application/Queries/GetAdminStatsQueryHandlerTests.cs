using Api.BoundedContexts.Administration.Application.Queries;
using Api.Models;
using Api.Services;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Tests for GetAdminStatsQueryHandler.
/// Tests delegation to AdminStatsService for dashboard statistics.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAdminStatsQueryHandlerTests
{
    private readonly Mock<IAdminStatsService> _mockAdminStatsService;
    private readonly GetAdminStatsQueryHandler _handler;

    public GetAdminStatsQueryHandlerTests()
    {
        _mockAdminStatsService = new Mock<IAdminStatsService>();
        _handler = new GetAdminStatsQueryHandler(_mockAdminStatsService.Object);
    }

    [Fact]
    public async Task Handle_WithDateRange_ReturnsDashboardStats()
    {
        // Arrange
        var fromDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var query = new GetAdminStatsQuery(fromDate, toDate);

        var expectedStats = new DashboardStatsDto(
            Metrics: new DashboardMetrics(
                TotalUsers: 150,
                ActiveSessions: 42,
                ApiRequestsToday: 0,
                TotalPdfDocuments: 75,
                TotalChatMessages: 1250,
                AverageConfidenceScore: 0.95,
                TotalRagRequests: 320,
                TotalTokensUsed: 0,
                // Issue #874: New metrics with test defaults
                TotalGames: 25,
                ApiRequests7d: 500,
                ApiRequests30d: 2000,
                AverageLatency24h: 200.0,
                AverageLatency7d: 215.0,
                ErrorRate24h: 0.05,
                ActiveAlerts: 2,
                ResolvedAlerts: 15,
                // Issue #3694: Extended KPIs
                TokenBalanceEur: 450m,
                TokenLimitEur: 1000m,
                DbStorageGb: 2.3m,
                DbStorageLimitGb: 10m,
                DbGrowthMbPerDay: 50m,
                CacheHitRatePercent: 94.2,
                CacheHitRateTrendPercent: 2.1
            ),
            UserTrend: new List<TimeSeriesDataPoint>(),
            SessionTrend: new List<TimeSeriesDataPoint>(),
            ApiRequestTrend: new List<TimeSeriesDataPoint>(),
            PdfUploadTrend: new List<TimeSeriesDataPoint>(),
            ChatMessageTrend: new List<TimeSeriesDataPoint>(),
            GeneratedAt: DateTime.UtcNow
        );

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p =>
                    p.FromDate == fromDate &&
                    p.ToDate == toDate),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(150, result.Metrics.TotalUsers);
        Assert.Equal(75, result.Metrics.TotalPdfDocuments);
        Assert.Equal(320, result.Metrics.TotalRagRequests);
        Assert.Equal(1250, result.Metrics.TotalChatMessages);
        _mockAdminStatsService.Verify(
            s => s.GetDashboardStatsAsync(
                It.IsAny<AnalyticsQueryParams>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithDaysParameter_PassesDaysToService()
    {
        // Arrange
        var query = new GetAdminStatsQuery(Days: 30);
        var expectedStats = new DashboardStatsDto(
            Metrics: new DashboardMetrics(
                TotalUsers: 100,
                ActiveSessions: 0,
                ApiRequestsToday: 0,
                TotalPdfDocuments: 50,
                TotalChatMessages: 0,
                AverageConfidenceScore: 0.0,
                TotalRagRequests: 0,
                TotalTokensUsed: 0,
                // Issue #874: New metrics
                TotalGames: 10,
                ApiRequests7d: 0,
                ApiRequests30d: 0,
                AverageLatency24h: 0.0,
                AverageLatency7d: 0.0,
                ErrorRate24h: 0.0,
                ActiveAlerts: 0,
                ResolvedAlerts: 0,
                // Issue #3694: Extended KPIs
                TokenBalanceEur: 450m,
                TokenLimitEur: 1000m,
                DbStorageGb: 2.3m,
                DbStorageLimitGb: 10m,
                DbGrowthMbPerDay: 50m,
                CacheHitRatePercent: 94.2,
                CacheHitRateTrendPercent: 2.1
            ),
            UserTrend: new List<TimeSeriesDataPoint>(),
            SessionTrend: new List<TimeSeriesDataPoint>(),
            ApiRequestTrend: new List<TimeSeriesDataPoint>(),
            PdfUploadTrend: new List<TimeSeriesDataPoint>(),
            ChatMessageTrend: new List<TimeSeriesDataPoint>(),
            GeneratedAt: DateTime.UtcNow
        );

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p => p.Days == 30),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        _mockAdminStatsService.Verify(
            s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p => p.Days == 30),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithGameIdFilter_PassesGameIdToService()
    {
        // Arrange
        var gameId = "catan-123";
        var query = new GetAdminStatsQuery(GameId: gameId);
        var expectedStats = new DashboardStatsDto(
            Metrics: new DashboardMetrics(
                TotalUsers: 0,
                ActiveSessions: 0,
                ApiRequestsToday: 0,
                TotalPdfDocuments: 0,
                TotalChatMessages: 280,
                AverageConfidenceScore: 0.0,
                TotalRagRequests: 45,
                TotalTokensUsed: 0,
                // Issue #874: New metrics
                TotalGames: 1,
                ApiRequests7d: 100,
                ApiRequests30d: 500,
                AverageLatency24h: 180.0,
                AverageLatency7d: 190.0,
                ErrorRate24h: 0.02,
                ActiveAlerts: 0,
                ResolvedAlerts: 5,
                // Issue #3694: Extended KPIs
                TokenBalanceEur: 450m,
                TokenLimitEur: 1000m,
                DbStorageGb: 2.3m,
                DbStorageLimitGb: 10m,
                DbGrowthMbPerDay: 50m,
                CacheHitRatePercent: 94.2,
                CacheHitRateTrendPercent: 2.1
            ),
            UserTrend: new List<TimeSeriesDataPoint>(),
            SessionTrend: new List<TimeSeriesDataPoint>(),
            ApiRequestTrend: new List<TimeSeriesDataPoint>(),
            PdfUploadTrend: new List<TimeSeriesDataPoint>(),
            ChatMessageTrend: new List<TimeSeriesDataPoint>(),
            GeneratedAt: DateTime.UtcNow
        );

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p => p.GameId == gameId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(45, result.Metrics.TotalRagRequests);
        _mockAdminStatsService.Verify(
            s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p => p.GameId == gameId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithRoleFilter_PassesRoleToService()
    {
        // Arrange
        var roleFilter = "Admin";
        var query = new GetAdminStatsQuery(RoleFilter: roleFilter);
        var expectedStats = new DashboardStatsDto(
            Metrics: new DashboardMetrics(
                TotalUsers: 5,
                ActiveSessions: 3,
                ApiRequestsToday: 0,
                TotalPdfDocuments: 0,
                TotalChatMessages: 0,
                AverageConfidenceScore: 0.0,
                TotalRagRequests: 0,
                TotalTokensUsed: 0,
                // Issue #874: New metrics
                TotalGames: 0,
                ApiRequests7d: 0,
                ApiRequests30d: 0,
                AverageLatency24h: 0.0,
                AverageLatency7d: 0.0,
                ErrorRate24h: 0.0,
                ActiveAlerts: 0,
                ResolvedAlerts: 0,
                // Issue #3694: Extended KPIs
                TokenBalanceEur: 450m,
                TokenLimitEur: 1000m,
                DbStorageGb: 2.3m,
                DbStorageLimitGb: 10m,
                DbGrowthMbPerDay: 50m,
                CacheHitRatePercent: 94.2,
                CacheHitRateTrendPercent: 2.1
            ),
            UserTrend: new List<TimeSeriesDataPoint>(),
            SessionTrend: new List<TimeSeriesDataPoint>(),
            ApiRequestTrend: new List<TimeSeriesDataPoint>(),
            PdfUploadTrend: new List<TimeSeriesDataPoint>(),
            ChatMessageTrend: new List<TimeSeriesDataPoint>(),
            GeneratedAt: DateTime.UtcNow
        );

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p => p.RoleFilter == roleFilter),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Metrics.TotalUsers);
        _mockAdminStatsService.Verify(
            s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p => p.RoleFilter == roleFilter),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAllParameters_PassesAllToService()
    {
        // Arrange
        var fromDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var gameId = "wingspan-456";
        var roleFilter = "User";
        var query = new GetAdminStatsQuery(fromDate, toDate, 30, gameId, roleFilter);

        var expectedStats = new DashboardStatsDto(
            Metrics: new DashboardMetrics(
                TotalUsers: 125,
                ActiveSessions: 0,
                ApiRequestsToday: 0,
                TotalPdfDocuments: 1,
                TotalChatMessages: 0,
                AverageConfidenceScore: 0.0,
                TotalRagRequests: 230,
                TotalTokensUsed: 0,
                // Issue #874: New metrics
                TotalGames: 0,
                ApiRequests7d: 0,
                ApiRequests30d: 0,
                AverageLatency24h: 0.0,
                AverageLatency7d: 0.0,
                ErrorRate24h: 0.0,
                ActiveAlerts: 0,
                ResolvedAlerts: 0,
                // Issue #3694: Extended KPIs
                TokenBalanceEur: 450m,
                TokenLimitEur: 1000m,
                DbStorageGb: 2.3m,
                DbStorageLimitGb: 10m,
                DbGrowthMbPerDay: 50m,
                CacheHitRatePercent: 94.2,
                CacheHitRateTrendPercent: 2.1
            ),
            UserTrend: new List<TimeSeriesDataPoint>(),
            SessionTrend: new List<TimeSeriesDataPoint>(),
            ApiRequestTrend: new List<TimeSeriesDataPoint>(),
            PdfUploadTrend: new List<TimeSeriesDataPoint>(),
            ChatMessageTrend: new List<TimeSeriesDataPoint>(),
            GeneratedAt: DateTime.UtcNow
        );

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p =>
                    p.FromDate == fromDate &&
                    p.ToDate == toDate &&
                    p.GameId == gameId &&
                    p.RoleFilter == roleFilter),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(125, result.Metrics.TotalUsers);
        _mockAdminStatsService.Verify(
            s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p =>
                    p.FromDate == fromDate &&
                    p.ToDate == toDate &&
                    p.GameId == gameId &&
                    p.RoleFilter == roleFilter),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToService()
    {
        // Arrange
        var query = new GetAdminStatsQuery(Days: 7);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var expectedStats = new DashboardStatsDto(
            Metrics: new DashboardMetrics(
                TotalUsers: 50,
                ActiveSessions: 0,
                ApiRequestsToday: 0,
                TotalPdfDocuments: 0,
                TotalChatMessages: 0,
                AverageConfidenceScore: 0.0,
                TotalRagRequests: 0,
                TotalTokensUsed: 0,
                // Issue #874: New metrics
                TotalGames: 0,
                ApiRequests7d: 0,
                ApiRequests30d: 0,
                AverageLatency24h: 0.0,
                AverageLatency7d: 0.0,
                ErrorRate24h: 0.0,
                ActiveAlerts: 0,
                ResolvedAlerts: 0,
                // Issue #3694: Extended KPIs
                TokenBalanceEur: 450m,
                TokenLimitEur: 1000m,
                DbStorageGb: 2.3m,
                DbStorageLimitGb: 10m,
                DbGrowthMbPerDay: 50m,
                CacheHitRatePercent: 94.2,
                CacheHitRateTrendPercent: 2.1
            ),
            UserTrend: new List<TimeSeriesDataPoint>(),
            SessionTrend: new List<TimeSeriesDataPoint>(),
            ApiRequestTrend: new List<TimeSeriesDataPoint>(),
            PdfUploadTrend: new List<TimeSeriesDataPoint>(),
            ChatMessageTrend: new List<TimeSeriesDataPoint>(),
            GeneratedAt: DateTime.UtcNow
        );

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.IsAny<AnalyticsQueryParams>(),
                cancellationToken))
            .ReturnsAsync(expectedStats);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockAdminStatsService.Verify(
            s => s.GetDashboardStatsAsync(
                It.IsAny<AnalyticsQueryParams>(),
                cancellationToken),
            Times.Once);
    }

    // Issue #2911: Error Handling Tests
    [Fact]
    public async Task Handle_WhenServiceThrowsException_PropagatesException()
    {
        // Arrange
        var query = new GetAdminStatsQuery(Days: 7);
        var expectedException = new InvalidOperationException("Database connection failed");

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.IsAny<AnalyticsQueryParams>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Equal("Database connection failed", exception.Message);
        _mockAdminStatsService.Verify(
            s => s.GetDashboardStatsAsync(
                It.IsAny<AnalyticsQueryParams>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenServiceThrowsTimeoutException_PropagatesException()
    {
        // Arrange
        var query = new GetAdminStatsQuery(Days: 30);
        var expectedException = new TimeoutException("Query timed out after 30 seconds");

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.IsAny<AnalyticsQueryParams>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Equal("Query timed out after 30 seconds", exception.Message);
    }

    [Fact]
    public async Task Handle_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var query = new GetAdminStatsQuery(Days: 7);
        cancellationTokenSource.Cancel();

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.IsAny<AnalyticsQueryParams>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _handler.Handle(query, cancellationTokenSource.Token));
    }

    // Issue #2911: Null Parameter Validation Tests
    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new GetAdminStatsQueryHandler(null!));

        Assert.Equal("adminStatsService", exception.ParamName);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    // Issue #2911: Metric Aggregation Logic Tests
    [Fact]
    public async Task Handle_WithComplexMetrics_VerifiesMetricAggregation()
    {
        // Arrange
        var query = new GetAdminStatsQuery(Days: 7);

        var timeSeriesData = new List<TimeSeriesDataPoint>
        {
            new(DateTime.UtcNow.AddDays(-6), 10),
            new(DateTime.UtcNow.AddDays(-5), 15),
            new(DateTime.UtcNow.AddDays(-4), 20),
            new(DateTime.UtcNow.AddDays(-3), 25),
            new(DateTime.UtcNow.AddDays(-2), 30),
            new(DateTime.UtcNow.AddDays(-1), 35),
            new(DateTime.UtcNow, 40)
        };

        var expectedStats = new DashboardStatsDto(
            Metrics: new DashboardMetrics(
                TotalUsers: 150,
                ActiveSessions: 42,
                ApiRequestsToday: 1250,
                TotalPdfDocuments: 75,
                TotalChatMessages: 3500,
                AverageConfidenceScore: 0.92,
                TotalRagRequests: 820,
                TotalTokensUsed: 150000,
                TotalGames: 45,
                ApiRequests7d: 8750,
                ApiRequests30d: 35000,
                AverageLatency24h: 185.5,
                AverageLatency7d: 195.7,
                ErrorRate24h: 0.015,
                ActiveAlerts: 3,
                ResolvedAlerts: 25,
                // Issue #3694: Extended KPIs
                TokenBalanceEur: 450m,
                TokenLimitEur: 1000m,
                DbStorageGb: 2.3m,
                DbStorageLimitGb: 10m,
                DbGrowthMbPerDay: 50m,
                CacheHitRatePercent: 94.2,
                CacheHitRateTrendPercent: 2.1
            ),
            UserTrend: timeSeriesData,
            SessionTrend: timeSeriesData,
            ApiRequestTrend: timeSeriesData,
            PdfUploadTrend: timeSeriesData,
            ChatMessageTrend: timeSeriesData,
            GeneratedAt: DateTime.UtcNow
        );

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.IsAny<AnalyticsQueryParams>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert - Verify all metrics are properly aggregated
        Assert.NotNull(result);
        Assert.Equal(150, result.Metrics.TotalUsers);
        Assert.Equal(42, result.Metrics.ActiveSessions);
        Assert.Equal(1250, result.Metrics.ApiRequestsToday);
        Assert.Equal(75, result.Metrics.TotalPdfDocuments);
        Assert.Equal(3500, result.Metrics.TotalChatMessages);
        Assert.Equal(0.92, result.Metrics.AverageConfidenceScore);
        Assert.Equal(820, result.Metrics.TotalRagRequests);
        Assert.Equal(150000, result.Metrics.TotalTokensUsed);
        Assert.Equal(45, result.Metrics.TotalGames);
        Assert.Equal(8750, result.Metrics.ApiRequests7d);
        Assert.Equal(35000, result.Metrics.ApiRequests30d);
        Assert.Equal(185.5, result.Metrics.AverageLatency24h);
        Assert.Equal(195.7, result.Metrics.AverageLatency7d);
        Assert.Equal(0.015, result.Metrics.ErrorRate24h);
        Assert.Equal(3, result.Metrics.ActiveAlerts);
        Assert.Equal(25, result.Metrics.ResolvedAlerts);

        // Verify time series data
        Assert.Equal(7, result.UserTrend.Count);
        Assert.Equal(7, result.SessionTrend.Count);
        Assert.Equal(7, result.ApiRequestTrend.Count);
        Assert.Equal(7, result.PdfUploadTrend.Count);
        Assert.Equal(7, result.ChatMessageTrend.Count);
    }

    [Fact]
    public async Task Handle_WithEmptyMetrics_ReturnsZeroValues()
    {
        // Arrange
        var query = new GetAdminStatsQuery(Days: 7);

        var expectedStats = new DashboardStatsDto(
            Metrics: new DashboardMetrics(
                TotalUsers: 0,
                ActiveSessions: 0,
                ApiRequestsToday: 0,
                TotalPdfDocuments: 0,
                TotalChatMessages: 0,
                AverageConfidenceScore: 0.0,
                TotalRagRequests: 0,
                TotalTokensUsed: 0,
                TotalGames: 0,
                ApiRequests7d: 0,
                ApiRequests30d: 0,
                AverageLatency24h: 0.0,
                AverageLatency7d: 0.0,
                ErrorRate24h: 0.0,
                ActiveAlerts: 0,
                ResolvedAlerts: 0,
                // Issue #3694: Extended KPIs
                TokenBalanceEur: 450m,
                TokenLimitEur: 1000m,
                DbStorageGb: 2.3m,
                DbStorageLimitGb: 10m,
                DbGrowthMbPerDay: 50m,
                CacheHitRatePercent: 94.2,
                CacheHitRateTrendPercent: 2.1
            ),
            UserTrend: new List<TimeSeriesDataPoint>(),
            SessionTrend: new List<TimeSeriesDataPoint>(),
            ApiRequestTrend: new List<TimeSeriesDataPoint>(),
            PdfUploadTrend: new List<TimeSeriesDataPoint>(),
            ChatMessageTrend: new List<TimeSeriesDataPoint>(),
            GeneratedAt: DateTime.UtcNow
        );

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.IsAny<AnalyticsQueryParams>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Metrics.TotalUsers);
        Assert.Equal(0, result.Metrics.ActiveSessions);
        Assert.Equal(0.0, result.Metrics.AverageConfidenceScore);
        Assert.Equal(0.0, result.Metrics.ErrorRate24h);
        Assert.Empty(result.UserTrend);
        Assert.Empty(result.SessionTrend);
    }
}
