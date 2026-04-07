using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.Infrastructure;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class TestPipelineConnectivityQueryHandlerTests
{
    private readonly IInfrastructureHealthService _healthService = Substitute.For<IInfrastructureHealthService>();
    private readonly TestPipelineConnectivityQueryHandler _handler;

    public TestPipelineConnectivityQueryHandlerTests()
    {
        _handler = new TestPipelineConnectivityQueryHandler(
            _healthService,
            NullLogger<TestPipelineConnectivityQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_AllHealthy_ReturnsSuccess()
    {
        foreach (var name in ServiceRegistry.PipelineChain)
        {
            _healthService.GetServiceHealthAsync(name, Arg.Any<CancellationToken>())
                .Returns(new ServiceHealthStatus(name, HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(20)));
        }

        var result = await _handler.Handle(new TestPipelineConnectivityQuery(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ServiceRegistry.PipelineChain.Count, result.Hops.Count);
        Assert.True(result.TotalLatencyMs > 0);
    }

    [Fact]
    public async Task Handle_ServiceDown_StopsAtFailure()
    {
        _healthService.GetServiceHealthAsync("postgres", Arg.Any<CancellationToken>())
            .Returns(new ServiceHealthStatus("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(10)));
        _healthService.GetServiceHealthAsync("redis", Arg.Any<CancellationToken>())
            .Returns(new ServiceHealthStatus("redis", HealthState.Unhealthy, "Connection refused", DateTime.UtcNow, TimeSpan.Zero));

        var result = await _handler.Handle(new TestPipelineConnectivityQuery(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(2, result.Hops.Count);
        Assert.Equal(ServiceHealthLevel.Down, result.Hops[1].Status);
        Assert.Equal("Connection refused", result.Hops[1].Error);
    }
}
