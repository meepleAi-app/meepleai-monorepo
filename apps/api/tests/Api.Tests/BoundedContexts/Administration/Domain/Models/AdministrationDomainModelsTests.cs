using Api.BoundedContexts.Administration.Domain.Models;
using Api.BoundedContexts.Administration.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Models;

/// <summary>
/// Tests for Administration domain models.
/// Issue #3025: Backend 90% Coverage Target - Phase 27
/// </summary>
[Trait("Category", "Unit")]
public sealed class AdministrationDomainModelsTests
{
    #region PrometheusMetricsSummary Tests

    [Fact]
    public void PrometheusMetricsSummary_SetsAllProperties()
    {
        // Act
        var summary = new PrometheusMetricsSummary(
            ApiRequestsLast24h: 150000,
            AvgLatencyMs: 45.5,
            ErrorRate: 0.02,
            LlmCostLast24h: 125.75);

        // Assert
        summary.ApiRequestsLast24h.Should().Be(150000);
        summary.AvgLatencyMs.Should().Be(45.5);
        summary.ErrorRate.Should().Be(0.02);
        summary.LlmCostLast24h.Should().Be(125.75);
    }

    [Fact]
    public void PrometheusMetricsSummary_WithZeroValues_Succeeds()
    {
        // Act
        var summary = new PrometheusMetricsSummary(
            ApiRequestsLast24h: 0,
            AvgLatencyMs: 0.0,
            ErrorRate: 0.0,
            LlmCostLast24h: 0.0);

        // Assert
        summary.ApiRequestsLast24h.Should().Be(0);
        summary.AvgLatencyMs.Should().Be(0.0);
        summary.ErrorRate.Should().Be(0.0);
        summary.LlmCostLast24h.Should().Be(0.0);
    }

    [Fact]
    public void PrometheusMetricsSummary_WithHighVolume_Succeeds()
    {
        // Act
        var summary = new PrometheusMetricsSummary(
            ApiRequestsLast24h: long.MaxValue,
            AvgLatencyMs: 999.99,
            ErrorRate: 1.0,
            LlmCostLast24h: 50000.00);

        // Assert
        summary.ApiRequestsLast24h.Should().Be(long.MaxValue);
        summary.ErrorRate.Should().Be(1.0); // 100% error rate
    }

    [Fact]
    public void PrometheusMetricsSummary_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var summary1 = new PrometheusMetricsSummary(100, 50.0, 0.01, 10.0);
        var summary2 = new PrometheusMetricsSummary(100, 50.0, 0.01, 10.0);
        var summary3 = new PrometheusMetricsSummary(200, 50.0, 0.01, 10.0);

        // Assert
        summary1.Should().Be(summary2);
        summary1.Should().NotBe(summary3);
    }

    [Fact]
    public void PrometheusMetricsSummary_RecordWith_CreatesModifiedCopy()
    {
        // Arrange
        var original = new PrometheusMetricsSummary(100, 50.0, 0.01, 10.0);

        // Act
        var modified = original with { ApiRequestsLast24h = 200 };

        // Assert
        modified.ApiRequestsLast24h.Should().Be(200);
        modified.AvgLatencyMs.Should().Be(50.0);
        original.ApiRequestsLast24h.Should().Be(100);
    }

    #endregion

    #region ServiceHealthStatus Tests

    [Fact]
    public void ServiceHealthStatus_SetsAllProperties()
    {
        // Arrange
        var checkedAt = DateTime.UtcNow;
        var responseTime = TimeSpan.FromMilliseconds(150);

        // Act
        var status = new ServiceHealthStatus(
            ServiceName: "postgres",
            State: HealthState.Healthy,
            ErrorMessage: null,
            CheckedAt: checkedAt,
            ResponseTime: responseTime);

        // Assert
        status.ServiceName.Should().Be("postgres");
        status.State.Should().Be(HealthState.Healthy);
        status.ErrorMessage.Should().BeNull();
        status.CheckedAt.Should().Be(checkedAt);
        status.ResponseTime.Should().Be(responseTime);
    }

    [Fact]
    public void ServiceHealthStatus_WithErrorMessage_SetsError()
    {
        // Arrange
        var checkedAt = DateTime.UtcNow;
        var responseTime = TimeSpan.FromSeconds(30);

        // Act
        var status = new ServiceHealthStatus(
            ServiceName: "redis",
            State: HealthState.Unhealthy,
            ErrorMessage: "Connection timeout after 30 seconds",
            CheckedAt: checkedAt,
            ResponseTime: responseTime);

        // Assert
        status.State.Should().Be(HealthState.Unhealthy);
        status.ErrorMessage.Should().Be("Connection timeout after 30 seconds");
    }

    [Fact]
    public void ServiceHealthStatus_WithDegradedState_SetsCorrectly()
    {
        // Act
        var status = new ServiceHealthStatus(
            ServiceName: "qdrant",
            State: HealthState.Degraded,
            ErrorMessage: "High latency detected",
            CheckedAt: DateTime.UtcNow,
            ResponseTime: TimeSpan.FromMilliseconds(5000));

        // Assert
        status.State.Should().Be(HealthState.Degraded);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("redis")]
    [InlineData("qdrant")]
    [InlineData("n8n")]
    [InlineData("prometheus")]
    public void ServiceHealthStatus_AcceptsAllServiceNames(string serviceName)
    {
        // Act
        var status = new ServiceHealthStatus(
            ServiceName: serviceName,
            State: HealthState.Healthy,
            ErrorMessage: null,
            CheckedAt: DateTime.UtcNow,
            ResponseTime: TimeSpan.FromMilliseconds(50));

        // Assert
        status.ServiceName.Should().Be(serviceName);
    }

    #endregion

    #region OverallHealthStatus Tests

    [Fact]
    public void OverallHealthStatus_SetsAllProperties()
    {
        // Arrange
        var checkedAt = DateTime.UtcNow;

        // Act
        var status = new OverallHealthStatus(
            State: HealthState.Healthy,
            TotalServices: 5,
            HealthyServices: 5,
            DegradedServices: 0,
            UnhealthyServices: 0,
            CheckedAt: checkedAt);

        // Assert
        status.State.Should().Be(HealthState.Healthy);
        status.TotalServices.Should().Be(5);
        status.HealthyServices.Should().Be(5);
        status.DegradedServices.Should().Be(0);
        status.UnhealthyServices.Should().Be(0);
        status.CheckedAt.Should().Be(checkedAt);
    }

    [Fact]
    public void OverallHealthStatus_WithDegradedServices_SetsDegradedState()
    {
        // Act
        var status = new OverallHealthStatus(
            State: HealthState.Degraded,
            TotalServices: 5,
            HealthyServices: 3,
            DegradedServices: 2,
            UnhealthyServices: 0,
            CheckedAt: DateTime.UtcNow);

        // Assert
        status.State.Should().Be(HealthState.Degraded);
        status.DegradedServices.Should().Be(2);
        (status.HealthyServices + status.DegradedServices + status.UnhealthyServices)
            .Should().Be(status.TotalServices);
    }

    [Fact]
    public void OverallHealthStatus_WithUnhealthyServices_SetsUnhealthyState()
    {
        // Act
        var status = new OverallHealthStatus(
            State: HealthState.Unhealthy,
            TotalServices: 5,
            HealthyServices: 2,
            DegradedServices: 1,
            UnhealthyServices: 2,
            CheckedAt: DateTime.UtcNow);

        // Assert
        status.State.Should().Be(HealthState.Unhealthy);
        status.UnhealthyServices.Should().Be(2);
    }

    [Fact]
    public void OverallHealthStatus_ServiceCountsSumToTotal()
    {
        // Arrange
        var status = new OverallHealthStatus(
            State: HealthState.Degraded,
            TotalServices: 10,
            HealthyServices: 6,
            DegradedServices: 3,
            UnhealthyServices: 1,
            CheckedAt: DateTime.UtcNow);

        // Assert
        var sum = status.HealthyServices + status.DegradedServices + status.UnhealthyServices;
        sum.Should().Be(status.TotalServices);
    }

    #endregion

    #region HealthState Enum Tests

    [Fact]
    public void HealthState_Healthy_HasValueZero()
    {
        // Assert
        ((int)HealthState.Healthy).Should().Be(0);
    }

    [Fact]
    public void HealthState_Degraded_HasValueOne()
    {
        // Assert
        ((int)HealthState.Degraded).Should().Be(1);
    }

    [Fact]
    public void HealthState_Unhealthy_HasValueTwo()
    {
        // Assert
        ((int)HealthState.Unhealthy).Should().Be(2);
    }

    [Fact]
    public void HealthState_HasThreeValues()
    {
        // Arrange
        var values = Enum.GetValues<HealthState>();

        // Assert
        values.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(0, "Healthy")]
    [InlineData(1, "Degraded")]
    [InlineData(2, "Unhealthy")]
    public void HealthState_HasCorrectNames(int stateValue, string expectedName)
    {
        // Arrange
        var state = (HealthState)stateValue;

        // Assert
        state.ToString().Should().Be(expectedName);
    }

    [Fact]
    public void HealthState_SeverityOrder_HealthyLessThanDegradedLessThanUnhealthy()
    {
        // The numeric values should increase with severity
        ((int)HealthState.Healthy).Should().BeLessThan((int)HealthState.Degraded);
        ((int)HealthState.Degraded).Should().BeLessThan((int)HealthState.Unhealthy);
    }

    #endregion

    #region InfrastructureDetails Tests

    [Fact]
    public void InfrastructureDetails_SetsAllProperties()
    {
        // Arrange
        var overall = new OverallHealthStatus(
            State: HealthState.Healthy,
            TotalServices: 3,
            HealthyServices: 3,
            DegradedServices: 0,
            UnhealthyServices: 0,
            CheckedAt: DateTime.UtcNow);

        var services = new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(50)),
            new("redis", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(10)),
            new("qdrant", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(100))
        };

        var metrics = new PrometheusMetricsSummary(
            ApiRequestsLast24h: 50000,
            AvgLatencyMs: 35.0,
            ErrorRate: 0.005,
            LlmCostLast24h: 75.50);

        // Act
        var details = new InfrastructureDetails(overall, services, metrics);

        // Assert
        details.Overall.Should().Be(overall);
        details.Services.Should().HaveCount(3);
        details.Metrics.Should().Be(metrics);
    }

    [Fact]
    public void InfrastructureDetails_WithEmptyServices_Succeeds()
    {
        // Arrange
        var overall = new OverallHealthStatus(
            State: HealthState.Unhealthy,
            TotalServices: 0,
            HealthyServices: 0,
            DegradedServices: 0,
            UnhealthyServices: 0,
            CheckedAt: DateTime.UtcNow);

        var services = Array.Empty<ServiceHealthStatus>();
        var metrics = new PrometheusMetricsSummary(0, 0, 0, 0);

        // Act
        var details = new InfrastructureDetails(overall, services, metrics);

        // Assert
        details.Services.Should().BeEmpty();
        details.Overall.TotalServices.Should().Be(0);
    }

    [Fact]
    public void InfrastructureDetails_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var overall = new OverallHealthStatus(HealthState.Healthy, 1, 1, 0, 0, DateTime.UtcNow);
        var services = new List<ServiceHealthStatus>
        {
            new("test", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.Zero)
        };
        var metrics = new PrometheusMetricsSummary(100, 10, 0, 0);

        var details1 = new InfrastructureDetails(overall, services, metrics);
        var details2 = new InfrastructureDetails(overall, services, metrics);

        // Assert - same reference for services means equality
        details1.Should().Be(details2);
    }

    [Fact]
    public void InfrastructureDetails_ServicesAreReadOnly()
    {
        // Arrange
        var overall = new OverallHealthStatus(HealthState.Healthy, 1, 1, 0, 0, DateTime.UtcNow);
        var services = new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(50))
        };
        var metrics = new PrometheusMetricsSummary(100, 10, 0, 0);

        // Act
        var details = new InfrastructureDetails(overall, services, metrics);

        // Assert - Services is IReadOnlyCollection
        details.Services.Should().BeAssignableTo<IReadOnlyCollection<ServiceHealthStatus>>();
    }

    #endregion
}
