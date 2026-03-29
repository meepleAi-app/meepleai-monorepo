using Api.Infrastructure.Health.Checks;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Health;

[Trait("Category", "Unit")]
public sealed class EmbeddingDimensionHealthCheckTests
{
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<ILogger<EmbeddingDimensionHealthCheck>> _mockLogger;

    public EmbeddingDimensionHealthCheckTests()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockLogger = new Mock<ILogger<EmbeddingDimensionHealthCheck>>();
    }

    [Fact]
    public async Task Should_Return_Healthy_When_Dimensions_Match_Schema()
    {
        // Arrange — default schema is 768
        _mockEmbeddingService
            .Setup(s => s.GetEmbeddingDimensions())
            .Returns(768);

        var healthCheck = new EmbeddingDimensionHealthCheck(
            _mockEmbeddingService.Object, _mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(1536, "OpenRouterSmall")]
    [InlineData(1024, "External/HuggingFace")]
    [InlineData(3072, "OpenRouterLarge")]
    public async Task Should_Return_Unhealthy_When_Dimensions_Mismatch(
        int providerDimensions, string scenario)
    {
        // Arrange
        _mockEmbeddingService
            .Setup(s => s.GetEmbeddingDimensions())
            .Returns(providerDimensions);

        var healthCheck = new EmbeddingDimensionHealthCheck(
            _mockEmbeddingService.Object, _mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy,
            because: $"provider returns {providerDimensions} dims but schema expects 768 ({scenario})");
        result.Description.Should().Contain("768");
        result.Description.Should().Contain(providerDimensions.ToString());
    }

    [Fact]
    public async Task Should_Return_Degraded_When_Service_Unavailable()
    {
        // Arrange — service throws when not configured
        _mockEmbeddingService
            .Setup(s => s.GetEmbeddingDimensions())
            .Throws(new InvalidOperationException("Embedding service not configured"));

        var healthCheck = new EmbeddingDimensionHealthCheck(
            _mockEmbeddingService.Object, _mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("unavailable");
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }
}
