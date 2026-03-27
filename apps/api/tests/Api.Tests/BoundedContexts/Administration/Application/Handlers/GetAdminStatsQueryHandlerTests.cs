using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Models;
using Api.Services;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

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
        result.Should().NotBeNull();
        result.Metrics.TotalUsers.Should().Be(150);
        result.Metrics.TotalPdfDocuments.Should().Be(75);
        result.Metrics.TotalRagRequests.Should().Be(320);
        result.Metrics.TotalChatMessages.Should().Be(1250);
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
        result.Should().NotBeNull();
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
        result.Should().NotBeNull();
        result.Metrics.TotalRagRequests.Should().Be(45);
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
        result.Should().NotBeNull();
        result.Metrics.TotalUsers.Should().Be(5);
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
        result.Should().NotBeNull();
        result.Metrics.TotalUsers.Should().Be(125);
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
        var act = () =>
            _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be("Database connection failed");
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
        var act = () =>
            _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<TimeoutException>()).Which;

        exception.Message.Should().Be("Query timed out after 30 seconds");
    }

    [Fact]
    public async Task Handle_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var query = new GetAdminStatsQuery(Days: 7);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.IsAny<AnalyticsQueryParams>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var act = () =>
            _handler.Handle(query, cancellationTokenSource.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // Issue #2911: Null Parameter Validation Tests
    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new GetAdminStatsQueryHandler(null!);
        var exception = act.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("adminStatsService");
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
        result.Should().NotBeNull();
        result.Metrics.TotalUsers.Should().Be(150);
        result.Metrics.ActiveSessions.Should().Be(42);
        result.Metrics.ApiRequestsToday.Should().Be(1250);
        result.Metrics.TotalPdfDocuments.Should().Be(75);
        result.Metrics.TotalChatMessages.Should().Be(3500);
        result.Metrics.AverageConfidenceScore.Should().Be(0.92);
        result.Metrics.TotalRagRequests.Should().Be(820);
        result.Metrics.TotalTokensUsed.Should().Be(150000);
        result.Metrics.TotalGames.Should().Be(45);
        result.Metrics.ApiRequests7d.Should().Be(8750);
        result.Metrics.ApiRequests30d.Should().Be(35000);
        result.Metrics.AverageLatency24h.Should().Be(185.5);
        result.Metrics.AverageLatency7d.Should().Be(195.7);
        result.Metrics.ErrorRate24h.Should().Be(0.015);
        result.Metrics.ActiveAlerts.Should().Be(3);
        result.Metrics.ResolvedAlerts.Should().Be(25);

        // Verify time series data
        result.UserTrend.Count.Should().Be(7);
        result.SessionTrend.Count.Should().Be(7);
        result.ApiRequestTrend.Count.Should().Be(7);
        result.PdfUploadTrend.Count.Should().Be(7);
        result.ChatMessageTrend.Count.Should().Be(7);
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
        result.Should().NotBeNull();
        result.Metrics.TotalUsers.Should().Be(0);
        result.Metrics.ActiveSessions.Should().Be(0);
        result.Metrics.AverageConfidenceScore.Should().Be(0.0);
        result.Metrics.ErrorRate24h.Should().Be(0.0);
        result.UserTrend.Should().BeEmpty();
        result.SessionTrend.Should().BeEmpty();
    }
}
