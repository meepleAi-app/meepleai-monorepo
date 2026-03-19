using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Tests for GetPrometheusMetricsQueryHandler.
/// Issue #893: Prometheus HTTP API client integration tests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetPrometheusMetricsQueryHandlerTests
{
    private readonly Mock<IPrometheusQueryService> _mockPrometheusService;
    private readonly Mock<ILogger<GetPrometheusMetricsQueryHandler>> _mockLogger;
    private readonly GetPrometheusMetricsQueryHandler _handler;

    public GetPrometheusMetricsQueryHandlerTests()
    {
        _mockPrometheusService = new Mock<IPrometheusQueryService>();
        _mockLogger = new Mock<ILogger<GetPrometheusMetricsQueryHandler>>();
        _handler = new GetPrometheusMetricsQueryHandler(_mockPrometheusService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidRangeQuery_ReturnsMetrics()
    {
        // Arrange
        var start = DateTime.UtcNow.AddHours(-24);
        var end = DateTime.UtcNow;
        var query = new GetPrometheusMetricsQuery
        {
            Query = "sum(increase(meepleai_llm_cost_usd[1d])) by (model_id)",
            Start = start,
            End = end,
            Step = "1h"
        };

        var dataPoints = new List<PrometheusDataPoint>
        {
            new(DateTime.UtcNow.AddHours(-2), 10.5),
            new(DateTime.UtcNow.AddHours(-1), 12.3),
            new(DateTime.UtcNow, 15.7)
        };

        var timeSeries = new List<PrometheusTimeSeries>
        {
            new(
                new Dictionary<string, string> { { "model_id", "gpt-4" } },
                dataPoints)
        };

        var prometheusResult = new PrometheusQueryResult("matrix", timeSeries);

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(
                query.Query,
                query.Start,
                query.End,
                query.Step,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prometheusResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("matrix", result.ResultType);
        Assert.Single(result.TimeSeries);

        var ts = result.TimeSeries.ElementAt(0);
        Assert.Equal("gpt-4", ts.Metric["model_id"]);
        Assert.Equal(3, ts.Values.Count);
        Assert.Equal(15.7, ts.Values.Last().Value);

        _mockPrometheusService.Verify(
            s => s.QueryRangeAsync(query.Query, query.Start, query.End, query.Step, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyResult_ReturnsEmptyTimeSeries()
    {
        // Arrange
        var query = new GetPrometheusMetricsQuery
        {
            Query = "nonexistent_metric",
            Start = DateTime.UtcNow.AddHours(-1),
            End = DateTime.UtcNow,
            Step = "5m"
        };

        var prometheusResult = new PrometheusQueryResult("matrix", Array.Empty<PrometheusTimeSeries>());

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

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.TimeSeries);
    }

    [Fact]
    public async Task Handle_WithMultipleTimeSeries_ReturnsAllSeries()
    {
        // Arrange
        var query = new GetPrometheusMetricsQuery
        {
            Query = "sum(rate(http_requests_total[5m])) by (status)",
            Start = DateTime.UtcNow.AddMinutes(-30),
            End = DateTime.UtcNow,
            Step = "5m"
        };

        var timeSeries = new List<PrometheusTimeSeries>
        {
            new(
                new Dictionary<string, string> { { "status", "200" } },
                new List<PrometheusDataPoint> { new(DateTime.UtcNow, 95.5) }),
            new(
                new Dictionary<string, string> { { "status", "500" } },
                new List<PrometheusDataPoint> { new(DateTime.UtcNow, 4.5) })
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

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TimeSeries.Count);

        var status200 = result.TimeSeries.First(ts => ts.Metric.ContainsKey("status") && ts.Metric["status"] == "200");
        var status500 = result.TimeSeries.First(ts => ts.Metric.ContainsKey("status") && ts.Metric["status"] == "500");

        Assert.Equal(95.5, status200.Values.ElementAt(0).Value);
        Assert.Equal(4.5, status500.Values.ElementAt(0).Value);
    }

    [Fact]
    public async Task Handle_WhenPrometheusServiceThrows_PropagatesException()
    {
        // Arrange
        var query = new GetPrometheusMetricsQuery
        {
            Query = "invalid_query",
            Start = DateTime.UtcNow.AddHours(-1),
            End = DateTime.UtcNow,
            Step = "5m"
        };

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Prometheus query failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(query, CancellationToken.None));

        _mockPrometheusService.Verify(
            s => s.QueryRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidQuery_LogsInformation()
    {
        // Arrange
        var query = new GetPrometheusMetricsQuery
        {
            Query = "up",
            Start = DateTime.UtcNow.AddMinutes(-5),
            End = DateTime.UtcNow,
            Step = "1m"
        };

        var prometheusResult = new PrometheusQueryResult("matrix", Array.Empty<PrometheusTimeSeries>());

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prometheusResult);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Executing Prometheus range query")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
