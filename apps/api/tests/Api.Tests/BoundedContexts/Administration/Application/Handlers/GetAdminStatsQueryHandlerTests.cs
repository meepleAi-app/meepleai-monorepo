using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Models;
using Api.Services;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetAdminStatsQueryHandler.
/// Tests delegation to AdminStatsService for dashboard statistics.
/// </summary>
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
                TotalTokensUsed: 0
            ),
            UserTrend: Array.Empty<TimeSeriesDataPoint>(),
            SessionTrend: Array.Empty<TimeSeriesDataPoint>(),
            ApiRequestTrend: Array.Empty<TimeSeriesDataPoint>(),
            PdfUploadTrend: Array.Empty<TimeSeriesDataPoint>(),
            ChatMessageTrend: Array.Empty<TimeSeriesDataPoint>(),
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
                TotalTokensUsed: 0
            ),
            UserTrend: Array.Empty<TimeSeriesDataPoint>(),
            SessionTrend: Array.Empty<TimeSeriesDataPoint>(),
            ApiRequestTrend: Array.Empty<TimeSeriesDataPoint>(),
            PdfUploadTrend: Array.Empty<TimeSeriesDataPoint>(),
            ChatMessageTrend: Array.Empty<TimeSeriesDataPoint>(),
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
                TotalTokensUsed: 0
            ),
            UserTrend: Array.Empty<TimeSeriesDataPoint>(),
            SessionTrend: Array.Empty<TimeSeriesDataPoint>(),
            ApiRequestTrend: Array.Empty<TimeSeriesDataPoint>(),
            PdfUploadTrend: Array.Empty<TimeSeriesDataPoint>(),
            ChatMessageTrend: Array.Empty<TimeSeriesDataPoint>(),
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
                TotalTokensUsed: 0
            ),
            UserTrend: Array.Empty<TimeSeriesDataPoint>(),
            SessionTrend: Array.Empty<TimeSeriesDataPoint>(),
            ApiRequestTrend: Array.Empty<TimeSeriesDataPoint>(),
            PdfUploadTrend: Array.Empty<TimeSeriesDataPoint>(),
            ChatMessageTrend: Array.Empty<TimeSeriesDataPoint>(),
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
                TotalTokensUsed: 0
            ),
            UserTrend: Array.Empty<TimeSeriesDataPoint>(),
            SessionTrend: Array.Empty<TimeSeriesDataPoint>(),
            ApiRequestTrend: Array.Empty<TimeSeriesDataPoint>(),
            PdfUploadTrend: Array.Empty<TimeSeriesDataPoint>(),
            ChatMessageTrend: Array.Empty<TimeSeriesDataPoint>(),
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
        var cancellationTokenSource = new CancellationTokenSource();
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
                TotalTokensUsed: 0
            ),
            UserTrend: Array.Empty<TimeSeriesDataPoint>(),
            SessionTrend: Array.Empty<TimeSeriesDataPoint>(),
            ApiRequestTrend: Array.Empty<TimeSeriesDataPoint>(),
            PdfUploadTrend: Array.Empty<TimeSeriesDataPoint>(),
            ChatMessageTrend: Array.Empty<TimeSeriesDataPoint>(),
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
}

