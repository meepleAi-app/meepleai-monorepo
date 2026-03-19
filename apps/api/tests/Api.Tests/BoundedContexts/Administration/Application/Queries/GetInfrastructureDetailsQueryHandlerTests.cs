using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Models;
using Api.BoundedContexts.Administration.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Issue #894: Tests for GetInfrastructureDetailsQueryHandler
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetInfrastructureDetailsQueryHandlerTests
{
    private readonly Mock<IInfrastructureDetailsService> _mockDetailsService;
    private readonly Mock<ILogger<GetInfrastructureDetailsQueryHandler>> _mockLogger;
    private readonly GetInfrastructureDetailsQueryHandler _handler;

    public GetInfrastructureDetailsQueryHandlerTests()
    {
        _mockDetailsService = new Mock<IInfrastructureDetailsService>();
        _mockLogger = new Mock<ILogger<GetInfrastructureDetailsQueryHandler>>();
        _handler = new GetInfrastructureDetailsQueryHandler(_mockDetailsService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnInfrastructureDetails_WhenServiceSucceeds()
    {
        // Arrange
        var query = new GetInfrastructureDetailsQuery();
        var expectedDetails = CreateSampleInfrastructureDetails();

        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedDetails.Overall.State, result.Overall.State);
        Assert.Equal(expectedDetails.Services.Count, result.Services.Count);
        Assert.Equal(expectedDetails.Metrics.ApiRequestsLast24h, result.Metrics.ApiRequestsLast24h);
    }

    [Fact]
    public async Task Handle_ShouldCallServiceOnce_WhenInvoked()
    {
        // Arrange
        var query = new GetInfrastructureDetailsQuery();
        var expectedDetails = CreateSampleInfrastructureDetails();

        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockDetailsService.Verify(
            s => s.GetDetailsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenServiceFails()
    {
        // Arrange
        var query = new GetInfrastructureDetailsQuery();
        var expectedException = new InvalidOperationException("Service failure");

        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(query, CancellationToken.None));

        Assert.Equal("Service failure", exception.Message);
    }

    [Fact]
    public async Task Handle_ShouldLogInformation_WhenSuccessful()
    {
        // Arrange
        var query = new GetInfrastructureDetailsQuery();
        var expectedDetails = CreateSampleInfrastructureDetails();

        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Handling GetInfrastructureDetailsQuery")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Infrastructure details retrieved successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static InfrastructureDetails CreateSampleInfrastructureDetails()
    {
        var overall = new OverallHealthStatus(
            HealthState.Healthy,
            TotalServices: 6,
            HealthyServices: 6,
            DegradedServices: 0,
            UnhealthyServices: 0,
            DateTime.UtcNow
        );

        var services = new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(45)),
            new("redis", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(30))
        };

        var metrics = new PrometheusMetricsSummary(
            ApiRequestsLast24h: 15430,
            AvgLatencyMs: 120.5,
            ErrorRate: 0.02,
            LlmCostLast24h: 3.45
        );

        return new InfrastructureDetails(overall, services, metrics);
    }
}
