using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetMetricsTimeSeriesQueryHandler.
/// Issue #901: Time-series endpoint for infrastructure charts.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetMetricsTimeSeriesQueryHandlerTests
{
    private readonly Mock<IPrometheusQueryService> _mockPrometheusService;
    private readonly Mock<ILogger<GetMetricsTimeSeriesQueryHandler>> _mockLogger;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly GetMetricsTimeSeriesQueryHandler _handler;

    public GetMetricsTimeSeriesQueryHandlerTests()
    {
        _mockPrometheusService = new Mock<IPrometheusQueryService>();
        _mockLogger = new Mock<ILogger<GetMetricsTimeSeriesQueryHandler>>();
        _fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 18, 12, 0, 0, TimeSpan.Zero));
        _handler = new GetMetricsTimeSeriesQueryHandler(
            _mockPrometheusService.Object, _mockLogger.Object, _fakeTimeProvider);
    }

    [Fact]
    public async Task Handle_WithOneHourRange_ExecutesThreeQueriesInParallel()
    {
        // Arrange
        var query = new GetMetricsTimeSeriesQuery { Range = MetricsTimeRange.OneHour };
        SetupPrometheusServiceReturnsDataPoints(3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Cpu.Count);
        Assert.Equal(3, result.Memory.Count);
        Assert.Equal(3, result.Requests.Count);

        // Verify all 3 queries were executed
        _mockPrometheusService.Verify(
            s => s.QueryRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                "5m",
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Theory]
    [InlineData(0, "5m")]   // OneHour
    [InlineData(1, "15m")]  // SixHours
    [InlineData(2, "30m")]  // TwentyFourHours
    [InlineData(3, "1h")]   // SevenDays
    public async Task Handle_WithDifferentRanges_UsesCorrectStep(int rangeValue, string expectedStep)
    {
        // Arrange
        var range = (MetricsTimeRange)rangeValue;
        var query = new GetMetricsTimeSeriesQuery { Range = range };
        SetupPrometheusServiceReturnsDataPoints(1);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockPrometheusService.Verify(
            s => s.QueryRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                expectedStep,
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_WhenCpuQueryFails_ReturnsEmptyCpuWithOtherData()
    {
        // Arrange
        var query = new GetMetricsTimeSeriesQuery { Range = MetricsTimeRange.OneHour };

        // CPU query fails
        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(
                It.Is<string>(q => q.Contains("node_cpu_seconds_total")),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Prometheus unreachable"));

        // Memory and requests succeed
        var dataPoints = new List<PrometheusDataPoint>
        {
            new(DateTime.UtcNow, 42.0)
        };
        var timeSeries = new List<PrometheusTimeSeries>
        {
            new(new Dictionary<string, string>(), dataPoints)
        };
        var successResult = new PrometheusQueryResult("matrix", timeSeries);

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(
                It.Is<string>(q => !q.Contains("node_cpu_seconds_total")),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - graceful degradation: CPU empty, others have data
        Assert.NotNull(result);
        Assert.Empty(result.Cpu);
        Assert.Single(result.Memory);
        Assert.Single(result.Requests);
    }

    [Fact]
    public async Task Handle_WhenAllQueriesFail_ReturnsEmptySeriesForAll()
    {
        // Arrange
        var query = new GetMetricsTimeSeriesQuery { Range = MetricsTimeRange.OneHour };

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Prometheus down"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - all empty, no exception thrown
        Assert.NotNull(result);
        Assert.Empty(result.Cpu);
        Assert.Empty(result.Memory);
        Assert.Empty(result.Requests);
    }

    [Fact]
    public async Task Handle_WithEmptyPrometheusResult_ReturnsEmptySeries()
    {
        // Arrange
        var query = new GetMetricsTimeSeriesQuery { Range = MetricsTimeRange.SixHours };

        var emptyResult = new PrometheusQueryResult("matrix", Array.Empty<PrometheusTimeSeries>());

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Cpu);
        Assert.Empty(result.Memory);
        Assert.Empty(result.Requests);
    }

    [Fact]
    public async Task Handle_DataPointsAreOrderedByTimestamp()
    {
        // Arrange
        var query = new GetMetricsTimeSeriesQuery { Range = MetricsTimeRange.OneHour };

        var now = DateTime.UtcNow;
        var unorderedDataPoints = new List<PrometheusDataPoint>
        {
            new(now, 30.0),
            new(now.AddMinutes(-10), 10.0),
            new(now.AddMinutes(-5), 20.0)
        };
        var timeSeries = new List<PrometheusTimeSeries>
        {
            new(new Dictionary<string, string>(), unorderedDataPoints)
        };
        var prometheusResult = new PrometheusQueryResult("matrix", timeSeries);

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prometheusResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - verify data points are ordered by timestamp
        var cpuPoints = result.Cpu.ToList();
        Assert.Equal(3, cpuPoints.Count);
        Assert.Equal(10.0, cpuPoints[0].Value);
        Assert.Equal(20.0, cpuPoints[1].Value);
        Assert.Equal(30.0, cpuPoints[2].Value);
    }

    [Fact]
    public async Task Handle_WithSevenDayRange_PassesCorrectTimeBoundaries()
    {
        // Arrange
        var query = new GetMetricsTimeSeriesQuery { Range = MetricsTimeRange.SevenDays };
        SetupPrometheusServiceReturnsDataPoints(1);

        var expectedEnd = _fakeTimeProvider.GetUtcNow().UtcDateTime;
        var expectedStart = expectedEnd - TimeSpan.FromDays(7);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - verify correct time boundaries are passed to Prometheus
        _mockPrometheusService.Verify(
            s => s.QueryRangeAsync(
                It.IsAny<string>(),
                It.Is<DateTime>(d => Math.Abs((d - expectedStart).TotalSeconds) < 1),
                It.Is<DateTime>(d => Math.Abs((d - expectedEnd).TotalSeconds) < 1),
                "1h",
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    private void SetupPrometheusServiceReturnsDataPoints(int count)
    {
        var now = _fakeTimeProvider.GetUtcNow().UtcDateTime;
        var dataPoints = Enumerable.Range(0, count)
            .Select(i => new PrometheusDataPoint(now.AddMinutes(-count + i), 50.0 + i))
            .ToList();

        var timeSeries = new List<PrometheusTimeSeries>
        {
            new(new Dictionary<string, string>(), dataPoints)
        };
        var prometheusResult = new PrometheusQueryResult("matrix", timeSeries);

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prometheusResult);
    }
}
