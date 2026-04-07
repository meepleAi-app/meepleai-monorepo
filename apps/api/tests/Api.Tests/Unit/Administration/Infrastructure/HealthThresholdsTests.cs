using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class HealthThresholdsTests
{
    [Fact]
    public void DetermineHealth_HealthyService_ReturnsHealthy()
    {
        var result = HealthThresholds.DetermineHealth("embedding", true, 500, 1.0);
        Assert.Equal(ServiceHealthLevel.Healthy, result);
    }

    [Fact]
    public void DetermineHealth_HighLatency_ReturnsDegraded()
    {
        var result = HealthThresholds.DetermineHealth("embedding", true, 3000, 1.0);
        Assert.Equal(ServiceHealthLevel.Degraded, result);
    }

    [Fact]
    public void DetermineHealth_HighErrorRate_ReturnsDegraded()
    {
        var result = HealthThresholds.DetermineHealth("embedding", true, 500, 8.0);
        Assert.Equal(ServiceHealthLevel.Degraded, result);
    }

    [Fact]
    public void DetermineHealth_CriticalErrorRate_ReturnsDown()
    {
        var result = HealthThresholds.DetermineHealth("embedding", true, 500, 25.0);
        Assert.Equal(ServiceHealthLevel.Down, result);
    }

    [Fact]
    public void DetermineHealth_HealthCheckFailed_ReturnsDown()
    {
        var result = HealthThresholds.DetermineHealth("embedding", false, 0, 0);
        Assert.Equal(ServiceHealthLevel.Down, result);
    }

    [Fact]
    public void DetermineHealth_Redis_LowThreshold()
    {
        var degraded = HealthThresholds.DetermineHealth("redis", true, 150, 0);
        Assert.Equal(ServiceHealthLevel.Degraded, degraded);

        var healthy = HealthThresholds.DetermineHealth("redis", true, 50, 0);
        Assert.Equal(ServiceHealthLevel.Healthy, healthy);
    }
}
