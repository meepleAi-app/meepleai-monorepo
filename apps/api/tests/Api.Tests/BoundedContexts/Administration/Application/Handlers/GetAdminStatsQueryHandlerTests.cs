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

        var expectedStats = new DashboardStatsDto
        {
            TotalUsers = 150,
            TotalGames = 75,
            TotalSessions = 320,
            TotalQuestions = 1250,
            ActiveUsers = 42,
            QuestionsLast24h = 89,
            AverageResponseTime = 1.2,
            SystemHealth = "Healthy"
        };

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p =>
                    p.FromDate == fromDate &&
                    p.ToDate == toDate),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(150, result.TotalUsers);
        Assert.Equal(75, result.TotalGames);
        Assert.Equal(320, result.TotalSessions);
        Assert.Equal(1250, result.TotalQuestions);
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
        var expectedStats = new DashboardStatsDto
        {
            TotalUsers = 100,
            TotalGames = 50
        };

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p => p.Days == 30),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

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
        var expectedStats = new DashboardStatsDto
        {
            TotalSessions = 45,
            TotalQuestions = 280
        };

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p => p.GameId == gameId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(45, result.TotalSessions);
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
        var expectedStats = new DashboardStatsDto
        {
            TotalUsers = 5,
            ActiveUsers = 3
        };

        _mockAdminStatsService
            .Setup(s => s.GetDashboardStatsAsync(
                It.Is<AnalyticsQueryParams>(p => p.RoleFilter == roleFilter),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.TotalUsers);
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
        var query = new GetAdminStatsQuery(fromDate, toDate, null, gameId, roleFilter);

        var expectedStats = new DashboardStatsDto
        {
            TotalUsers = 125,
            TotalGames = 1,
            TotalSessions = 230
        };

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
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(125, result.TotalUsers);
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

        var expectedStats = new DashboardStatsDto
        {
            TotalUsers = 50
        };

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
