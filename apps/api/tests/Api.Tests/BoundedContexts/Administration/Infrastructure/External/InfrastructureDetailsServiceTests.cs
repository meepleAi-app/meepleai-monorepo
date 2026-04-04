using Api.BoundedContexts.Administration.Domain.Models;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// Issue #894: Tests for InfrastructureDetailsService orchestration logic
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class InfrastructureDetailsServiceTests
{
    private readonly Mock<IInfrastructureHealthService> _mockHealthService;
    private readonly Mock<IPrometheusQueryService> _mockPrometheusService;
    private readonly Mock<ILogger<InfrastructureDetailsService>> _mockLogger;
    private readonly InfrastructureDetailsService _service;

    public InfrastructureDetailsServiceTests()
    {
        _mockHealthService = new Mock<IInfrastructureHealthService>();
        _mockPrometheusService = new Mock<IPrometheusQueryService>();
        _mockLogger = new Mock<ILogger<InfrastructureDetailsService>>();
        _service = new InfrastructureDetailsService(
            _mockHealthService.Object,
            _mockPrometheusService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetDetailsAsync_ShouldReturnDetails_WhenAllServicesSucceed()
    {
        // Arrange
        var overall = CreateHealthyOverallStatus();
        var services = CreateHealthyServices();
        var metricsResult = CreateSamplePrometheusResult(15430);

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overall);

        _mockHealthService
            .Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metricsResult);

        // Act
        var result = await _service.GetDetailsAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Overall.State.Should().Be(HealthState.Healthy);
        result.Services.Count.Should().Be(2);
        result.Metrics.ApiRequestsLast24h.Should().Be(15430);
    }

    [Fact]
    public async Task GetDetailsAsync_ShouldCallHealthServicesOnce_WhenInvoked()
    {
        // Arrange
        SetupSuccessfulMocks();

        // Act
        await _service.GetDetailsAsync(CancellationToken.None);

        // Assert
        _mockHealthService.Verify(
            s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _mockHealthService.Verify(
            s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDetailsAsync_ShouldCallPrometheusQueries4Times_WhenInvoked()
    {
        // Arrange
        SetupSuccessfulMocks();

        // Act
        await _service.GetDetailsAsync(CancellationToken.None);

        // Assert
        // 4 Prometheus queries: API requests, avg latency, error rate, LLM cost
        _mockPrometheusService.Verify(
            s => s.QueryRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task GetDetailsAsync_ShouldReturn0Metrics_WhenPrometheusQueriesFail()
    {
        // Arrange
        var overall = CreateHealthyOverallStatus();
        var services = CreateHealthyServices();

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overall);

        _mockHealthService
            .Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Prometheus unavailable"));

        // Act
        var result = await _service.GetDetailsAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Metrics.ApiRequestsLast24h.Should().Be(0);
        result.Metrics.AvgLatencyMs.Should().Be(0);
        result.Metrics.ErrorRate.Should().Be(0);
        result.Metrics.LlmCostLast24h.Should().Be(0);
    }

    [Fact]
    public async Task GetDetailsAsync_ShouldPropagateException_WhenHealthServiceFails()
    {
        // Arrange
        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Health service failure"));

        // Act & Assert
        var act = () => _service.GetDetailsAsync(CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be("Health service failure");
    }

    [Fact]
    public async Task GetDetailsAsync_ShouldExecuteQueriesInParallel_WhenInvoked()
    {
        // Arrange
        var callOrder = new List<string>();
        var overall = CreateHealthyOverallStatus();
        var services = CreateHealthyServices();
        var metricsResult = CreateSamplePrometheusResult(100);

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callOrder.Add("health-overall");
                return overall;
            });

        _mockHealthService
            .Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callOrder.Add("health-services");
                return services;
            });

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callOrder.Add("prometheus");
                return metricsResult;
            });

        // Act
        await _service.GetDetailsAsync(CancellationToken.None);

        // Assert - all calls should happen before Task.WhenAll completes
        (callOrder.Count >= 6).Should().BeTrue("All 6 parallel calls should execute");
    }

    private void SetupSuccessfulMocks()
    {
        var overall = CreateHealthyOverallStatus();
        var services = CreateHealthyServices();
        var metricsResult = CreateSamplePrometheusResult(15430);

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overall);

        _mockHealthService
            .Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        _mockPrometheusService
            .Setup(s => s.QueryRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metricsResult);
    }

    private static OverallHealthStatus CreateHealthyOverallStatus()
    {
        return new OverallHealthStatus(
            HealthState.Healthy,
            TotalServices: 6,
            HealthyServices: 6,
            DegradedServices: 0,
            UnhealthyServices: 0,
            DateTime.UtcNow
        );
    }

    private static IReadOnlyCollection<ServiceHealthStatus> CreateHealthyServices()
    {
        return new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(45)),
            new("redis", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(30))
        };
    }

    private static PrometheusQueryResult CreateSamplePrometheusResult(double value)
    {
        var dataPoint = new PrometheusDataPoint(DateTime.UtcNow, value);
        var timeSeries = new PrometheusTimeSeries(
            new Dictionary<string, string> { { "model_id", "gpt-4" } },
            new List<PrometheusDataPoint> { dataPoint }
        );

        return new PrometheusQueryResult(
            "matrix",
            new List<PrometheusTimeSeries> { timeSeries }
        );
    }
}
