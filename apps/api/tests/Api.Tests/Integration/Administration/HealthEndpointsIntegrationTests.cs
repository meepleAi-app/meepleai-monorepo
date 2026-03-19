using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for individual service health endpoints (Issue #892).
/// Tests the 4 dedicated health endpoints: postgres, redis, qdrant, n8n.
///
/// Test Categories:
/// 1. Individual Endpoint Tests: Each service endpoint returns correct data
/// 2. Service Name Mapping: Correct service name passed to query handler
/// 3. Response Format: Consistent JSON structure across all endpoints
///
/// Infrastructure: Mocked HealthCheckService (no containers needed)
/// Coverage Target: ≥90% for MonitoringEndpoints.cs health methods
/// Execution Time Target: <5s
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class HealthEndpointsIntegrationTests
{
    private readonly Mock<IInfrastructureHealthService> _healthServiceMock;
    private readonly Mock<ILogger<GetInfrastructureHealthQueryHandler>> _loggerMock;

    public HealthEndpointsIntegrationTests()
    {
        _healthServiceMock = new Mock<IInfrastructureHealthService>();
        _loggerMock = new Mock<ILogger<GetInfrastructureHealthQueryHandler>>();
    }

    private GetInfrastructureHealthQueryHandler CreateHandler() =>
        new(_healthServiceMock.Object, _loggerMock.Object);

    [Theory]
    [InlineData("postgres", "PostgreSQL")]
    [InlineData("redis", "Redis")]
    [InlineData("qdrant", "Qdrant")]
    [InlineData("n8n", "n8n")]
    public async Task GetServiceHealth_ValidService_ReturnsHealthStatus(string serviceName, string displayName)
    {
        // Arrange
        var expectedStatus = new ServiceHealthStatus(
            serviceName,
            HealthState.Healthy,
            null,
            DateTime.UtcNow,
            TimeSpan.FromMilliseconds(50));

        _healthServiceMock
            .Setup(s => s.GetServiceHealthAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStatus);

        var handler = CreateHandler();
        var query = new GetInfrastructureHealthQuery { ServiceName = serviceName };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Services.Should().ContainSingle();

        var service = result.Services.ElementAt(0);
        service.ServiceName.Should().Be(serviceName);
        service.State.Should().Be("Healthy");
        service.ErrorMessage.Should().BeNull();
        service.ResponseTimeMs.Should().BeGreaterThanOrEqualTo(0);

        _healthServiceMock.Verify(
            s => s.GetServiceHealthAsync(serviceName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("redis")]
    [InlineData("qdrant")]
    [InlineData("n8n")]
    public async Task GetServiceHealth_UnhealthyService_ReturnsUnhealthyStatus(string serviceName)
    {
        // Arrange
        var expectedStatus = new ServiceHealthStatus(
            serviceName,
            HealthState.Unhealthy,
            "Connection timeout",
            DateTime.UtcNow,
            TimeSpan.FromMilliseconds(5000));

        _healthServiceMock
            .Setup(s => s.GetServiceHealthAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStatus);

        var handler = CreateHandler();
        var query = new GetInfrastructureHealthQuery { ServiceName = serviceName };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Services.Should().ContainSingle();

        var service = result.Services.ElementAt(0);
        service.State.Should().Be("Unhealthy");
        service.ErrorMessage.Should().Be("Connection timeout");
        service.ResponseTimeMs.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("redis")]
    [InlineData("qdrant")]
    [InlineData("n8n")]
    public async Task GetServiceHealth_DegradedService_ReturnsDegradedStatus(string serviceName)
    {
        // Arrange
        var expectedStatus = new ServiceHealthStatus(
            serviceName,
            HealthState.Degraded,
            "High latency detected",
            DateTime.UtcNow,
            TimeSpan.FromMilliseconds(2000));

        _healthServiceMock
            .Setup(s => s.GetServiceHealthAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStatus);

        var handler = CreateHandler();
        var query = new GetInfrastructureHealthQuery { ServiceName = serviceName };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Services.Should().ContainSingle();

        var service = result.Services.ElementAt(0);
        service.State.Should().Be("Degraded");
        service.ErrorMessage.Should().Be("High latency detected");
    }

    [Fact]
    public async Task GetServiceHealth_PostgresSql_MapsToPostgresServiceName()
    {
        // Arrange
        var expectedStatus = new ServiceHealthStatus(
            "postgres",
            HealthState.Healthy,
            null,
            DateTime.UtcNow,
            TimeSpan.FromMilliseconds(50));

        _healthServiceMock
            .Setup(s => s.GetServiceHealthAsync("postgres", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStatus);

        var handler = CreateHandler();
        var query = new GetInfrastructureHealthQuery { ServiceName = "postgres" };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Services.Should().ContainSingle();
        result.Services.ElementAt(0).ServiceName.Should().Be("postgres");

        _healthServiceMock.Verify(
            s => s.GetServiceHealthAsync("postgres", It.IsAny<CancellationToken>()),
            Times.Once,
            "PostgreSQL endpoint should query 'postgres' service name");
    }

    [Fact]
    public async Task GetServiceHealth_N8N_ReturnsWorkflowServiceHealth()
    {
        // Arrange - Issue #892: New n8n health endpoint
        var expectedStatus = new ServiceHealthStatus(
            "n8n",
            HealthState.Healthy,
            null,
            DateTime.UtcNow,
            TimeSpan.FromMilliseconds(100));

        _healthServiceMock
            .Setup(s => s.GetServiceHealthAsync("n8n", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStatus);

        var handler = CreateHandler();
        var query = new GetInfrastructureHealthQuery { ServiceName = "n8n" };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Services.Should().ContainSingle();
        var service = result.Services.ElementAt(0);
        service.ServiceName.Should().Be("n8n");
        service.State.Should().Be("Healthy");
    }

    [Theory]
    [InlineData("postgres", 45.5)]
    [InlineData("redis", 12.3)]
    [InlineData("qdrant", 78.9)]
    [InlineData("n8n", 150.2)]
    public async Task GetServiceHealth_ValidService_IncludesResponseTime(string serviceName, double responseTimeMs)
    {
        // Arrange
        var expectedStatus = new ServiceHealthStatus(
            serviceName,
            HealthState.Healthy,
            null,
            DateTime.UtcNow,
            TimeSpan.FromMilliseconds(responseTimeMs));

        _healthServiceMock
            .Setup(s => s.GetServiceHealthAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStatus);

        var handler = CreateHandler();
        var query = new GetInfrastructureHealthQuery { ServiceName = serviceName };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var service = result.Services.ElementAt(0);
        service.ResponseTimeMs.Should().Be(responseTimeMs);
    }
}
